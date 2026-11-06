using Peckworks.Screensavers.Core;

namespace Thanksgiving.Happenings;

/// <summary>
/// A murmuration: a few hundred starlings wheeling over the far hills at
/// dusk as one cloud, the cloud stretching, folding, turning over itself and
/// drifting, the way they do before they settle to roost. It fades in from
/// nothing, wheels for most of a minute, and fades away.
///
/// FEYNMAN VERSION: no bird steers. Each bird keeps a fixed place inside a
/// ball (an offset rolled once, in Begin). The BALL is what moves: its centre
/// drifts on a path made of slow sines, it turns about its centre, and it is
/// squashed and stretched by two more slow sines (long and thin one moment,
/// round the next, the way a real flock pulses), and a ripple runs through
/// it (each bird's offset wobbles by a sine of time and of where it is in
/// the ball, so a wave seems to pass through the flock). Add those up and
/// the eye reads one living shape, which is all a murmuration is from a
/// mile off. Every bird is one dark dot, two pixels, stamped at its place;
/// nothing is painted per frame.
///
/// It is drawn through the painter's OpenFromFarm stencil (in front of the
/// hills, the farm and the far trees, behind everything on the field), and
/// lives over the far hills between the house and the barn, high enough to
/// be sky and low enough to read as birds and not stars.
/// </summary>
internal sealed class Murmuration : Happening
{
    private const int Birds = 260;
    private const float Total = 46f;

    private readonly ThanksgivingScenery _s;
    private readonly Sprite _dot;
    private readonly float[] _ox = new float[Birds], _oy = new float[Birds], _ph = new float[Birds];
    private PointF _c0;
    private float _r, _p1, _p2, _p3, _p4;

    public override float Seconds => Total;
    public override string? Claims => "sky";

    public Murmuration(ThanksgivingScenery s)
    {
        _s = s;
        int d = Math.Max(2, (int)(s.U * 0.0016f));
        _dot = Sprite.Paint(d + 1, d + 1, g =>
        {
            using var b = new SolidBrush(Color.FromArgb(220, 34, 26, 34));
            g.FillEllipse(b, 0, 0, d, d);
        });
    }

    public override void Begin(Random rng)
    {
        float W = _s.Width, H = _s.Height, U = _s.U;
        // Over the far hills, in the gap between the farmhouse and the barn, a little above the ridge.
        _c0 = new PointF(W * (0.40f + 0.12f * rng.NextSingle()), H * (0.47f + 0.06f * rng.NextSingle()));
        _r = U * (0.045f + 0.015f * rng.NextSingle());
        _p1 = rng.NextSingle() * MathF.Tau; _p2 = rng.NextSingle() * MathF.Tau; _p3 = rng.NextSingle() * MathF.Tau; _p4 = rng.NextSingle() * MathF.Tau;
        // The birds' places in the ball: denser toward the middle (the square root of a uniform roll is a disc's
        // even spread; its 0.7 power leans the spread inward), thinner at the edge, each with its own ripple phase.
        for (int i = 0; i < Birds; i++)
        {
            float a = rng.NextSingle() * MathF.Tau, rr = MathF.Pow(rng.NextSingle(), 0.7f);
            _ox[i] = MathF.Cos(a) * rr; _oy[i] = MathF.Sin(a) * rr * 0.75f;
            _ph[i] = rng.NextSingle() * MathF.Tau;
        }
    }

    public override void Draw(FrameBuffer fb, float t)
    {
        float fade = Fade(t, Total, 4f, 4f);
        if (fade <= 0.01f) return;
        float U = _s.U;
        // The ball's centre drifts; the ball turns, stretches and pulses; a ripple runs through it.
        float cx = _c0.X + U * (0.10f * MathF.Sin(0.21f * t + _p1) + 0.04f * MathF.Sin(0.53f * t + _p2));
        float cy = _c0.Y + U * (0.030f * MathF.Sin(0.17f * t + _p3) + 0.012f * MathF.Sin(0.61f * t + _p4));
        float turn = 0.45f * t + 0.6f * MathF.Sin(0.3f * t + _p2);
        float sx = 1f + 0.55f * MathF.Sin(0.37f * t + _p3), sy = 1f - 0.35f * MathF.Sin(0.29f * t + _p4);
        float ct = MathF.Cos(turn), st = MathF.Sin(turn);
        for (int i = 0; i < Birds; i++)
        {
            float ripple = 0.14f * MathF.Sin(2.4f * t + 3.5f * _ox[i] + _ph[i]);
            float x = _ox[i] * sx + ripple, y = _oy[i] * sy + 0.5f * ripple;
            float px = cx + _r * (x * ct - y * st), py = cy + _r * (x * st + y * ct);
            _dot.DrawCentered(fb, px, py, fade, _s.OpenFromFarm);
        }
    }
}
