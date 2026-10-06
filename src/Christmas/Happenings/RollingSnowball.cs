using System.Drawing.Drawing2D;
using Peckworks.Screensavers.Core;

namespace Christmas.Happenings;

/// <summary>
/// A snowball rolls in from one side of the screen, picking up snow as it
/// goes, so it gets bigger and bigger. It leaves a faint darker track behind
/// it, slows down, wobbles to a stop near the middle, sits a moment, and
/// melts away.
///
/// How it is drawn:
///
///   - The ball is a sprite flip-book. 8 sizes (small to big) times 12 turning
///     poses. A pose is the same ball with a faint curved mark turned a
///     little further round, and the mark is what shows that it is ROLLING
///     and not just sliding. The shading (white on the moon side, grey-blue
///     on the other) does not turn, because the light does not turn.
///   - How far it has rolled, divided by its radius, is how far round it has
///     turned. (A wheel that rolls one circumference has turned once.)
///   - "f" is how far along the journey it is, 0 to 1. Everything (where it
///     is, how big, the track behind it) is worked out from f. Time becomes f
///     with an "ease out" curve: fast at the start, gentler and gentler.
///   - The track is a chain of short fb.Line pieces from the start to the
///     ball, each following the hill's height and as thick as the ball was
///     at that point.
///
/// The ball sits on the snow by asking the ground for its height at x. It
/// claims "snow" so no other happening shares the snow with it.
/// </summary>
internal sealed class RollingSnowball : Happening
{
    private const int Sizes = 8, Poses = 12;
    private const float RollStart = 0.15f, RollEnd = 5.0f;   // seconds: when it begins to roll and when it has stopped
    private static readonly Color Track = Color.FromArgb(128, 148, 196);

    private readonly ChristmasScenery _s;
    private readonly float _rMin, _rMax;
    private readonly float[] _radius = new float[Sizes];
    private readonly Sprite[,] _ball = new Sprite[Sizes, Poses];
    private readonly Sprite[] _shadow = new Sprite[Sizes];
    private float _x0, _x1, _dir;                           // start x, stop x, and +1 (rolling right) or -1 (rolling left)

    public override float Seconds => 8f;
    public override string? Claims => "snow";

    public RollingSnowball(ChristmasScenery s)
    {
        _s = s;
        _rMin = Math.Max(2.5f, s.U * 0.008f);
        _rMax = Math.Max(_rMin * 3, s.U * 0.04f);
        for (int i = 0; i < Sizes; i++)
        {
            float r = _rMin * MathF.Pow(_rMax / _rMin, i / (float)(Sizes - 1));   // sizes step up evenly in ratio, so no step looks bigger than the others
            _radius[i] = r;
            for (int p = 0; p < Poses; p++) _ball[i, p] = Ball(r, p * 360f / Poses);
            int sw = (int)(r * 3.0f) + 3, sh = (int)(r * 0.9f) + 3;
            _shadow[i] = Sprite.Paint(sw, sh, g =>
            {
                using var path = new GraphicsPath();
                path.AddEllipse(1, 1, sw - 2, sh - 2);
                using var br = new PathGradientBrush(path)
                {
                    CenterColor = Color.FromArgb(150, 70, 96, 156),
                    SurroundColors = [Color.FromArgb(0, 70, 96, 156)],
                    Blend = Sprite.SoftFalloff,
                };
                g.FillPath(br, path);
            });
        }
    }

    /// <summary>One snowball with its mark turned "degrees" round.</summary>
    private static Sprite Ball(float r, float degrees)
    {
        int pad = 3, d = (int)MathF.Ceiling(r * 2) + pad * 2;
        return Sprite.Paint(d, d, g =>
        {
            using var path = new GraphicsPath();
            path.AddEllipse(pad, pad, r * 2, r * 2);
            using (var shade = new SolidBrush(Color.FromArgb(255, 172, 192, 228)))
                g.FillPath(shade, path);

            var state = g.Save();
            g.SetClip(path, CombineMode.Intersect);

            // The white highlight on the moon side (does not turn).
            using var hi = new GraphicsPath();
            float hr = r * 1.15f;
            hi.AddEllipse(pad + r - r * 0.25f - hr, pad + r - r * 0.25f - hr, hr * 2, hr * 2);
            using (var glow = new PathGradientBrush(hi)
            {
                CenterColor = Color.White,
                SurroundColors = [Color.FromArgb(0, 255, 255, 255)],
                Blend = Sprite.SoftFalloff,
            })
                g.FillPath(glow, hi);

            // The marks (these DO turn): two curved lines and a lump, like the seams on a ball of packed snow.
            g.TranslateTransform(pad + r, pad + r);
            g.RotateTransform(degrees);
            using (var pen = new Pen(Color.FromArgb(170, 128, 150, 200), Math.Max(1f, r * 0.10f)))
            {
                g.DrawArc(pen, -r * 0.62f, -r * 0.62f, r * 1.24f, r * 1.24f, -70, 120);
                g.DrawArc(pen, -r * 0.30f, -r * 0.05f, r * 0.9f, r * 0.9f, 110, 80);
            }
            using (var lump = new SolidBrush(Color.FromArgb(120, 128, 150, 200)))
                g.FillEllipse(lump, -r * 0.55f, r * 0.25f, r * 0.28f, r * 0.2f);
            g.Restore(state);

            using var rim = new Pen(Color.FromArgb(150, 132, 156, 206), Math.Max(1f, r * 0.06f));
            g.DrawPath(rim, path);
        });
    }

    public override void Begin(Random rng)
    {
        _dir = rng.Next(2) == 0 ? 1 : -1;
        _x1 = _s.Width * (0.35f + 0.30f * (float)rng.NextDouble());
        _x0 = _dir > 0 ? -_rMax * 0.5f : _s.Width + _rMax * 0.5f;       // starts just off the edge
    }

    // Where the ball touches the snow. Ground.YAt is the hill's crest line, and a ball exactly on it looks
    // like it is floating against the valley behind. So it rolls a little way down the slope, in front of the crest.
    private float Snow(float x) => _s.Ground.YAt(x) + _s.U * 0.045f;

    private float XAt(float f) => _x0 + (_x1 - _x0) * f;
    private float RadiusAt(float f) => _rMin + (_rMax - _rMin) * f;     // it grows steadily as it picks up snow
    private int SizeFor(float r) => Math.Clamp((int)MathF.Round(MathF.Log(r / _rMin) / MathF.Log(_rMax / _rMin) * (Sizes - 1)), 0, Sizes - 1);

    public override void Draw(FrameBuffer fb, float t)
    {
        float fade = Fade(t, Seconds, 0.05f, 1.0f);

        float p = Math.Clamp((t - RollStart) / (RollEnd - RollStart), 0f, 1f);
        float f = 1 - (1 - p) * (1 - p);                                // ease out: quick at first, slowing to nothing
        float travel = MathF.Abs(_x1 - _x0) * f;                        // how far it has rolled, in pixels
        float r = RadiusAt(f);

        // The wobble to rest: after it stops it rocks a little, less and less.
        float wob = 0;
        if (t > RollEnd)
        {
            float a = t - RollEnd;
            wob = MathF.Exp(-a * 2.2f) * MathF.Sin(a * 11f);
        }
        float x = XAt(f) + _dir * r * 0.05f * wob;

        // ---- the track: pieces of line from the start to the ball ----
        if (f > 0.01f)
        {
            const int pieces = 18;
            for (int i = 0; i < pieces; i++)
            {
                float f0 = f * i / pieces, f1 = f * (i + 1) / pieces;
                float x0 = XAt(f0), x1 = XAt(f1);
                float thick = Math.Max(1.2f, RadiusAt(f1) * 0.7f);
                fb.Line(new PointF(x0, Snow(x0)), new PointF(x1 - _dir, Snow(x1)), Track, 0.25f * fade, thick);
            }
        }

        // ---- the ball ----
        int size = SizeFor(r);
        float turn = _dir * travel / r + wob * 0.30f;                   // radians turned: rolled distance over radius
        float frac = turn / MathF.Tau; frac -= MathF.Floor(frac);       // keep one lap, 0 to 1
        int pose = Math.Min(Poses - 1, (int)(frac * Poses));
        float rr = _radius[size];
        float gy = Snow(x);
        _shadow[size].DrawCentered(fb, x + rr * 0.3f, gy + rr * 0.08f, fade);
        _ball[size, pose].DrawCentered(fb, x, gy - rr * 0.9f, fade);
    }
}
