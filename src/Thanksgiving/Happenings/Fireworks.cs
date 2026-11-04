using Peckworks.Screensavers.Core;
using Peckworks.Screensavers.Core.Sakura;

namespace Thanksgiving.Happenings;

/// <summary>
/// Harvest fireworks over the far hills: two or three rockets climb from
/// behind the ridge, each trailing a thin sparkling tail, and burst into a
/// ball of glittering sparks that slow down, droop and crackle out. The
/// colours are the harvest's own: gold and amber, red and orange, and (once
/// in a while) a green one.
///
/// Each rocket has a start time. From it, everything is worked out from t:
///   - Climb (1.1 s): the rocket slides from the far ridge to its burst
///     point, fast at first and slowing as it rises, like a ball thrown up.
///   - Burst: a short bright flash, then 55 to 75 sparks fly out in all
///     directions. Each spark has its own direction, speed and twinkle
///     phase, rolled once in Begin.
///   - A spark's distance out is v * (1 - exp(-k * age)) / k: "air drag". It
///     starts at speed v and keeps slowing, creeping toward a final
///     distance, the way a paper plane coasts to a stop. Gravity then pulls
///     it down by half g times age squared.
///   - Each spark is a small glow with a short fading streak behind it. It
///     twinkles on its own rhythm and crackles in its last second.
///
/// Drawn only where the sky is open (the OpenSky stencil): the rockets rise
/// from BEHIND the hills and the corner branches stay in front. The bursts
/// are kept to the left and middle of the sky, away from the harvest moon,
/// whose own glow would drown them. Claims "skyshow" (the constellation
/// shares this sky) and "costly" (a burst of glows is the dearest thing a
/// sky happening does at 4K).
/// </summary>
internal sealed class Fireworks : Happening
{
    private const int MaxRockets = 3, MaxSparks = 80;
    private const float Climb = 1.1f, Life = 2.4f, Drag = 1.6f;     // seconds, seconds, and the drag strength per second

    // Colour schemes: two sparkle colours each (sprite 0 and sprite 1).
    private static readonly Color[][] Schemes =
    [
        [Color.FromArgb(255, 205, 90), Color.FromArgb(255, 150, 40)],     // gold and amber
        [Color.FromArgb(235, 60, 50), Color.FromArgb(255, 140, 50)],      // red and orange
        [Color.FromArgb(120, 225, 110), Color.FromArgb(255, 226, 130)],   // the one green, with gold glitter
    ];

    private readonly ThanksgivingScenery _s;
    private readonly Sprite[][] _dot = new Sprite[3][];            // per scheme, the two spark glows
    private readonly Sprite _flash, _head;

    private int _rockets;
    private float _seconds = 7f;
    private readonly float[] _launch = new float[MaxRockets];      // when each rocket leaves, in seconds
    private readonly PointF[] _from = new PointF[MaxRockets];      // where it leaves (on the far ridge)
    private readonly PointF[] _burst = new PointF[MaxRockets];     // where it bursts
    private readonly int[] _scheme = new int[MaxRockets];
    // Per spark (rocket * MaxSparks + i):
    private readonly int[] _count = new int[MaxRockets];
    private readonly float[] _dirX = new float[MaxRockets * MaxSparks], _dirY = new float[MaxRockets * MaxSparks];
    private readonly float[] _speed = new float[MaxRockets * MaxSparks];
    private readonly float[] _phase = new float[MaxRockets * MaxSparks], _rate = new float[MaxRockets * MaxSparks];
    private readonly int[] _which = new int[MaxRockets * MaxSparks];

    public override float Seconds => _seconds;
    public override string? Claims => "skyshow,costly";

    public Fireworks(ThanksgivingScenery s)
    {
        _s = s;
        int r = Math.Max(3, (int)(s.U * 0.017f));
        for (int i = 0; i < 3; i++) _dot[i] = [Sprite.Glow(r, Schemes[i][0]), Sprite.Glow(r, Schemes[i][1])];
        _flash = Sprite.Glow(Math.Max(4, (int)(s.U * 0.09f)), Color.FromArgb(255, 238, 200));
        _head = Sprite.Glow(Math.Max(3, (int)(s.U * 0.010f)), Color.FromArgb(255, 225, 160));
    }

    public override void Begin(Random rng)
    {
        _rockets = 2 + rng.Next(2);
        int greenAt = rng.Next(3) == 0 ? rng.Next(_rockets) : -1;      // a green shell in about one showing in three
        float t0 = 0.4f;
        for (int i = 0; i < _rockets; i++)
        {
            _launch[i] = t0;
            t0 += 1.3f + 0.4f * (float)rng.NextDouble();               // about 1.5 s apart
            _scheme[i] = i == greenAt ? 2 : rng.Next(2);

            // Away from the moon (at 0.74 w): the middle-left, 0.26 to 0.60 of the width (the left corner branch hides anything further left).
            float x = _s.Width * (0.26f + 0.34f * (float)rng.NextDouble());
            _from[i] = new PointF(x, Brushwork.RidgeYAt(_s.FarRidge, x));
            _burst[i] = new PointF(x + _s.Width * 0.05f * ((float)rng.NextDouble() * 2 - 1),
                                   _s.Height * (0.14f + 0.26f * (float)rng.NextDouble()));
            _burst[i].X = Math.Clamp(_burst[i].X, _s.Width * 0.22f, _s.Width * 0.64f);

            // The shell's size: how far the sparks finally reach, 0.15 to 0.21 U.
            float reach = _s.U * (0.15f + 0.06f * (float)rng.NextDouble());
            _count[i] = 55 + rng.Next(21);
            for (int j = 0; j < _count[i]; j++)
            {
                int k = i * MaxSparks + j;
                float a = (float)rng.NextDouble() * MathF.Tau;
                _dirX[k] = MathF.Cos(a); _dirY[k] = MathF.Sin(a);
                // A spread of speeds, leaning to the outside so the burst reads as a ball with some filling.
                float spread = 0.25f + 0.75f * MathF.Sqrt((float)rng.NextDouble());
                _speed[k] = reach * spread * Drag / (1 - MathF.Exp(-Drag * Life));
                _phase[k] = (float)rng.NextDouble() * MathF.Tau;
                _rate[k] = 14 + 16 * (float)rng.NextDouble();
                _which[k] = rng.NextDouble() < 0.55 ? 0 : 1;
            }
        }
        _seconds = _launch[_rockets - 1] + Climb + Life + 0.2f;
    }

    /// <summary>Where the rocket is k of the way up (0 to 1). Fast at first, slowing: 1 - (1-k)^2.</summary>
    private PointF Rocket(int r, float k)
    {
        float e = 1 - (1 - k) * (1 - k);
        return new PointF(_from[r].X + (_burst[r].X - _from[r].X) * e, _from[r].Y + (_burst[r].Y - _from[r].Y) * e);
    }

    /// <summary>Where a spark is, "age" seconds after the burst: drag outwards, gravity downwards.</summary>
    private PointF Spark(int r, int k, float age)
    {
        float dist = _speed[k] * (1 - MathF.Exp(-Drag * age)) / Drag;
        float droop = 0.5f * _s.U * 0.035f * age * age;
        return new PointF(_burst[r].X + _dirX[k] * dist, _burst[r].Y + _dirY[k] * dist + droop);
    }

    public override void Draw(FrameBuffer fb, float t)
    {
        float U = _s.U;
        for (int r = 0; r < _rockets; r++)
        {
            float up = t - _launch[r];
            if (up < 0) continue;

            if (up < Climb)
            {
                // ---- The climb: a bright head and a thin sparkling tail. ----
                float k = up / Climb;
                PointF head = Rocket(r, k);
                const int pieces = 12;
                for (int i = 0; i < pieces; i++)
                {
                    float near = 1 - i / (float)pieces;
                    PointF a = Rocket(r, Math.Max(0, k - 0.012f * i)), b = Rocket(r, Math.Max(0, k - 0.012f * (i + 1)));
                    fb.Line(a, b, Color.FromArgb(255, 214, 140), 0.9f * near, Math.Max(1.5f, U * 0.003f), _s.OpenSky);
                    // Glitter in the tail: a stray dot now and then, each on its own flicker.
                    if (i % 3 == 0 && MathF.Sin(up * 38 + i * 5.1f + r * 2) > 0.1f)
                        _head.DrawCentered(fb, b.X + U * 0.004f * MathF.Sin(i * 7.3f + r), b.Y, 0.55f * near, _s.OpenSky);
                }
                _head.DrawCentered(fb, head.X, head.Y, 1f, _s.OpenSky);
                continue;
            }

            // ---- The burst. ----
            float age = up - Climb;
            if (age >= Life) continue;

            // A brief bright flash, over in about a quarter of a second.
            float flash = 1 - age / 0.25f;
            if (flash > 0) _flash.DrawCentered(fb, _burst[r].X, _burst[r].Y, flash * flash, _s.OpenSky);

            float life = age / Life;                                       // 0 to 1
            float fade = MathF.Pow(1 - life, 0.9f);                        // the whole shell dims toward the end
            Color[] sc = Schemes[_scheme[r]];
            for (int j = 0; j < _count[r]; j++)
            {
                int k = r * MaxSparks + j;
                // Twinkle on its own phase; in the last second, crackle: on and off in short random-looking steps.
                float a = fade * (0.7f + 0.3f * MathF.Sin(age * _rate[k] + _phase[k]));
                if (age > Life - 1f)
                {
                    int step = (int)(age * 16);
                    uint h = (uint)(step * 7919 + k * 104729 + r * 31);
                    h ^= h >> 7; h *= 2654435761u; h ^= h >> 13;
                    if ((h & 3) == 0) a *= 0.1f;                           // about a quarter of the time it is nearly out
                    else if ((h & 3) == 1) a *= 1.5f;                      // and sometimes it pops brighter
                }
                a = Math.Min(1f, a);
                if (a <= 0.02f) continue;

                PointF p = Spark(r, k, age);
                PointF q = Spark(r, k, Math.Max(0, age - 0.08f));
                fb.Line(q, p, sc[_which[k]], 1.0f * a, Math.Max(2f, U * 0.0035f), _s.OpenSky);
                _dot[_scheme[r]][_which[k]].DrawCentered(fb, p.X, p.Y, a, _s.OpenSky);
            }
        }
    }
}
