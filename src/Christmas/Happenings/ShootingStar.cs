using Peckworks.Screensavers.Core;

namespace Christmas.Happenings;

/// <summary>
/// A shooting star to wish on: a bright point streaks down across the sky
/// with a tail behind it, drops a few sparks that twinkle and die, and
/// burns out. Warm white with a touch of gold, so it reads as a wish and
/// not as a bit of Halloween.
///
/// It is drawn "only where the sky is open" (the OpenSky stencil), so it
/// passes behind the mountains and the corner branches for free.
///
/// How it moves: the whole path is one straight line, decided in Begin. At
/// any moment, "k" says how far along that line the head is (0 = start,
/// 1 = end). Everything else is worked out from k: the tail is the stretch
/// of line just behind the head, and each spark lights up when k passes it.
/// </summary>
internal sealed class ShootingStar : Happening
{
    private const int Sparks = 7;
    private static readonly Color Gold = Color.FromArgb(255, 240, 200);

    private readonly ChristmasScenery _s;
    private readonly Sprite _head, _spark;
    private PointF _from, _travel;                         // where it starts, and the whole journey as (across, down)
    private readonly float[] _sparkAt = new float[Sparks]; // how far along the path each spark is dropped, 0 to 1
    private readonly float[] _sparkSide = new float[Sparks];// each spark's small sideways scatter, in pixels

    public override float Seconds => 1.7f;

    public ShootingStar(ChristmasScenery s)
    {
        _s = s;
        _head = Sprite.Glow((int)(s.U * 0.014f), Gold);
        _spark = Sprite.Glow((int)(s.U * 0.006f), Gold);
    }

    public override void Begin(Random rng)
    {
        float side = rng.Next(2) == 0 ? 1 : -1;                          // heading right or left
        float angle = 0.30f + 0.35f * (float)rng.NextDouble();           // how steeply it falls (radians below level)
        float length = _s.U * (0.45f + 0.25f * (float)rng.NextDouble());
        _travel = new PointF(side * MathF.Cos(angle) * length, MathF.Sin(angle) * length);

        // Pick where the MIDDLE of the path should be, then back up half a
        // journey to find the start. The middle stays in the right two
        // thirds of the sky: the moon is at the left, and a pale streak
        // across the pale moon, inside its glow, cannot be seen.
        float midX = _s.Width * (0.40f + 0.45f * (float)rng.NextDouble());
        _from = new PointF(midX - _travel.X / 2, _s.Height * (0.04f + 0.20f * (float)rng.NextDouble()));

        for (int i = 0; i < Sparks; i++)
        {
            _sparkAt[i] = 0.12f + 0.11f * i + 0.05f * (float)rng.NextDouble();
            _sparkSide[i] = _s.U * 0.006f * ((float)rng.NextDouble() * 2 - 1);
        }
    }

    private PointF At(float k) => new(_from.X + _travel.X * k, _from.Y + _travel.Y * k);

    public override void Draw(FrameBuffer fb, float t)
    {
        float k = t / Seconds;                                           // how far along the path, 0 to 1
        float fade = Fade(t, Seconds, 0.15f, 0.7f);                      // quick to appear, slower to burn out

        // The tail: 14 short pieces of line behind the head, each one
        // fainter and thinner than the one in front of it.
        const int pieces = 14;
        float tail = Math.Min(k, 0.36f);                                 // the tail grows as the star gets going
        for (int i = 0; i < pieces; i++)
        {
            float near = 1 - i / (float)pieces;                          // 1 at the head, falling toward 0 at the tail's end
            fb.Line(At(k - tail * i / pieces), At(k - tail * (i + 1) / pieces), Gold,
                    fade * near * MathF.Sqrt(near), Math.Max(1f, _s.U * 0.0045f * near), _s.OpenSky);
        }

        // The sparks it leaves behind. Each lives 0.6 seconds from the
        // moment the head passes it, sinking a little and twinkling
        // (a fast sine wave on its brightness) as it dies.
        for (int i = 0; i < Sparks; i++)
        {
            float age = (k - _sparkAt[i]) * Seconds;                     // seconds since the head went by
            if (age < 0 || age > 0.6f) continue;
            PointF p = At(_sparkAt[i]);
            float twinkle = 0.6f + 0.4f * MathF.Sin(age * 40 + i);
            _spark.DrawCentered(fb, p.X + _sparkSide[i], p.Y + _s.U * 0.05f * age * age,
                                fade * (1 - age / 0.6f) * twinkle, _s.OpenSky);
        }

        PointF head = At(k);
        _head.DrawCentered(fb, head.X, head.Y, fade, _s.OpenSky);
    }
}
