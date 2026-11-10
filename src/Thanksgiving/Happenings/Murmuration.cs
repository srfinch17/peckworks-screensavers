using Peckworks.Screensavers.Core;

namespace Thanksgiving.Happenings;

/// <summary>
/// A murmuration: a few hundred blackbirds wheeling over the far hills at
/// dusk as one flock, a long thin ribbon that streams along its way, bends,
/// folds with a wave running down it, bunches up and thins out, and turns
/// as its path turns, the way they do before they settle to roost. It fades
/// in from nothing, flies for most of a minute, and fades away.
///
/// FEYNMAN VERSION: no bird steers. The FLOCK is a ribbon laid along a
/// path: the leader's point moves along a curve made of slow sines, and
/// every other bird is where the leader WAS a little earlier (its place in
/// the ribbon is a lag in seconds, rolled once in Begin, plus a sideways
/// offset, most of them near the middle line). So the ribbon is the trail
/// the flock just flew: when the path bends, the ribbon BENDS along the
/// bend, and when the path slows at a turn the birds bunch up and then
/// stream out again, which is what a real murmuration does. (The version
/// before this laid the ribbon as a rigid bar along the path's direction at
/// that instant; each time the sines reversed, the bar spun half a turn about
/// its middle: "the bird flock full rotate seems awkward". The version before
/// THAT was a ball of dots turning about its centre: "a ball of points that
/// rotates". A thing laid along a trail never rotates about its middle.) On
/// top of that two things move through the ribbon as waves: a fold (a wave
/// running down its length, so it ripples like a flag) and a density wave
/// (the birds bunch and spread along it, a pulse travelling through the
/// flock); and each bird's place drifts a little with time (overtaking).
/// Each bird is a dark dot that flicks between two sizes on its own beat,
/// the twinkle a distant flock has.
///
/// It is drawn through the painter's OpenFromFarm stencil (in front of the
/// hills, the farm and the far trees, behind everything on the field), and
/// lives over the far hills between the house and the barn.
/// </summary>
internal sealed class Murmuration : Happening
{
    private const int Birds = 280;
    private const float Total = 50f;
    private const float Span = 3.6f;                 // seconds of path the ribbon covers from its leader to its last bird

    private readonly ThanksgivingScenery _s;
    private readonly Sprite _small, _big;
    private readonly float[] _along = new float[Birds], _across = new float[Birds], _ph = new float[Birds], _flap = new float[Birds];
    private PointF _c0;
    private float _ax, _ay, _p1, _p2, _p3, _p4, _p5;

    public override float Seconds => Total;
    public override string? Claims => "sky";

    public Murmuration(ThanksgivingScenery s)
    {
        _s = s;
        int d = Math.Max(2, (int)(s.U * 0.0014f));
        Sprite Dot(int size) => Sprite.Paint(size + 1, size + 1, g => { using var b = new SolidBrush(Color.FromArgb(230, 30, 24, 32)); g.FillEllipse(b, 0, 0, size, size); });
        _small = Dot(d);
        _big = Dot(d + 1);
    }

    public override void Begin(Random rng)
    {
        float W = _s.Width, H = _s.Height, U = _s.U;
        // Over the far hills between the farmhouse and the barn, a little above the ridge; the path's sweep in x and y.
        _c0 = new PointF(W * (0.42f + 0.10f * rng.NextSingle()), H * (0.46f + 0.05f * rng.NextSingle()));
        _ax = U * (0.14f + 0.06f * rng.NextSingle());
        _ay = U * (0.035f + 0.02f * rng.NextSingle());
        _p1 = rng.NextSingle() * MathF.Tau; _p2 = rng.NextSingle() * MathF.Tau; _p3 = rng.NextSingle() * MathF.Tau; _p4 = rng.NextSingle() * MathF.Tau; _p5 = rng.NextSingle() * MathF.Tau;
        for (int i = 0; i < Birds; i++)
        {
            // Along the ribbon evenly (-1 to 1); across it clustered on the middle line (the sum of two rolls, a tent shape).
            _along[i] = rng.NextSingle() * 2f - 1f;
            _across[i] = (rng.NextSingle() + rng.NextSingle() - 1f);
            _ph[i] = rng.NextSingle() * MathF.Tau;
            _flap[i] = rng.NextSingle() * MathF.Tau;
        }
    }

    /// <summary>The ribbon's middle at t, and its heading (the path's direction, from the sines' own rates).</summary>
    private (float X, float Y, float Hx, float Hy) Path(float t)
    {
        const float w1 = 0.19f, w2 = 0.47f, w3 = 0.15f, w4 = 0.53f;
        float x = _c0.X + _ax * MathF.Sin(w1 * t + _p1) + 0.35f * _ax * MathF.Sin(w2 * t + _p2);
        float y = _c0.Y + _ay * MathF.Sin(w3 * t + _p3) + 0.4f * _ay * MathF.Sin(w4 * t + _p4);
        float dx = _ax * w1 * MathF.Cos(w1 * t + _p1) + 0.35f * _ax * w2 * MathF.Cos(w2 * t + _p2);
        float dy = _ay * w3 * MathF.Cos(w3 * t + _p3) + 0.4f * _ay * w4 * MathF.Cos(w4 * t + _p4);
        float l = MathF.Max(1e-3f, MathF.Sqrt(dx * dx + dy * dy));
        return (x, y, dx / l, dy / l);
    }

    public override void Draw(FrameBuffer fb, float t)
    {
        float fade = Fade(t, Total, 4f, 4f);
        if (fade <= 0.01f) return;
        float U = _s.U;
        // The ribbon's width breathes slowly; a fold runs down it; the birds bunch in a travelling wave.
        float wid = U * (0.016f + 0.008f * MathF.Sin(0.31f * t + _p2));
        float foldPhase = 1.6f * t, bunchPhase = 1.1f * t;
        for (int i = 0; i < Birds; i++)
        {
            float a = _along[i] + 0.12f * MathF.Sin(0.5f * t + _ph[i]);                 // drifting along: overtaking
            a += 0.10f * MathF.Sin(MathF.PI * 1.5f * a - bunchPhase);                      // the density wave
            if (a > 1f) a -= 2f; else if (a < -1f) a += 2f;
            // This bird is where the leader was (1 - a) / 2 of the span ago: a = 1 is the leader, a = -1 the last bird.
            var (cx, cy, hx, hy) = Path(t - Span * (1f - a) * 0.5f);
            float nx = -hy, ny = hx;                                                    // across the path, at this bird's own point on it
            float b = _across[i] * (1f - 0.5f * a * a);                                   // the ribbon tapers toward its ends
            float across = b * wid + 0.6f * wid * MathF.Sin(3.1f * a + foldPhase + 0.3f * _ph[i]);   // the flag's fold
            float px = cx + nx * across, py = cy + ny * across;
            Sprite dot = MathF.Sin(9f * t + _flap[i]) > 0.35f ? _big : _small;           // the flicker of wings catching the light
            dot.DrawCentered(fb, px, py, fade, _s.OpenFromFarm);
        }
    }
}
