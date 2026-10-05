using System.Drawing.Drawing2D;
using Peckworks.Screensavers.Core;
using Peckworks.Screensavers.Core.Sakura;

namespace Halloween.Happenings;

/// <summary>
/// Halloween fireworks: two or three rockets climb from behind the far hill,
/// one after another, and burst into a ball of glittering sparks. Each rocket
/// is either purple-and-orange or all green.
///
/// THE STORY OF ONE ROCKET (how a real one behaves, which is what makes it
/// look right):
///   1. It leaves the ground fast and slows as it climbs, like a ball thrown
///      straight up. It spits a thin trail of sparkles behind it.
///   2. At the top it goes bang: one instant of bright flash.
///   3. About a hundred sparks fly out in every direction. Air pushes back on
///      them, so each one starts fast and quickly slows (drag). Gravity
///      pulls them down a little, so the ball droops like a willow.
///   4. Each spark twinkles on its own rhythm, then in the last second it
///      crackles (flickers on and off) as it dies.
///
/// Like every happening, Draw works from "t" alone. The sparks are not moved
/// a bit each frame. In Begin we write down each spark's direction, speed,
/// twinkle rhythm and lifetime. In Draw we ask "where is spark number 7 after
/// 1.3 seconds?" and a formula answers.
///
/// The drag formula: a spark thrown at speed v, with air drag k, has gone
///     distance = v * (1 - e^(-k * age)) / k
/// by "age" seconds. At age 0 it moves at speed v; as age grows, e^(-k*age)
/// shrinks toward 0 and the distance levels off at v / k. So v / k is how far
/// the spark can ever get: we pick that reach first, and work v from it.
///
/// Everything is stamped through the OpenSky stencil, so the rockets rise from
/// BEHIND the far hill and the bursts pass behind trees and branches.
/// </summary>
internal sealed class Fireworks : Happening
{
    private const int MaxRockets = 3;
    private const int SparksPerRocket = 100;
    private const float Climb = 1.1f;        // seconds from launch to bang
    private const float Drag = 2.2f;         // the "k" above: bigger = sparks slow sooner
    private const float StreakAge = 0.08f;   // each spark's streak reaches back this many seconds

    private static readonly Color Purple = Color.FromArgb(190, 90, 255);
    private static readonly Color Orange = Color.FromArgb(255, 140, 30);
    private static readonly Color Green = Color.FromArgb(90, 255, 90);
    private static readonly Color GlitterGreen = Color.FromArgb(215, 255, 215);   // pale green-white
    private static readonly Color Gold = Color.FromArgb(255, 205, 120);           // the rocket's own flame

    private readonly HalloweenScenery _s;
    private readonly Sprite _flash, _haloPurple, _haloGreen, _rocketHead, _trailSpark;
    private readonly Sprite[] _spark;        // 0 purple, 1 orange, 2 green, 3 glitter
    private readonly Color[] _sparkColor = [Purple, Orange, Green, GlitterGreen];

    // One rocket's dice, rolled in Begin.
    private sealed class Rocket
    {
        public float Launch;                 // when it leaves the ground, seconds into the showing
        public PointF From, Burst;           // where it starts (on the hill) and where it bangs
        public bool Green;                   // the color scheme
        public float Reach;                  // how far its sparks fly, in pixels
        public float[] DirX = new float[SparksPerRocket], DirY = new float[SparksPerRocket];
        public float[] Speed = new float[SparksPerRocket];   // 0..1, share of Reach
        public float[] Phase = new float[SparksPerRocket];   // where in its twinkle each spark begins
        public float[] Rate = new float[SparksPerRocket];    // how fast it twinkles, radians per second
        public float[] Life = new float[SparksPerRocket];    // how long it lasts, seconds
        public int[] Kind = new int[SparksPerRocket];        // which color sprite
    }

    private readonly List<Rocket> _rockets = [];
    private float _seconds = 8.5f;

    public override float Seconds => _seconds;

    public Fireworks(HalloweenScenery s)
    {
        _s = s;
        float u = s.U;

        // Each spark is a small glow; Sprite.Glow already puts a near-white
        // hot core in the middle, and that bright dot is the "sparkle".
        int r = Math.Max(2, (int)(u * 0.011f));
        _spark = new Sprite[_sparkColor.Length];
        for (int i = 0; i < _spark.Length; i++) _spark[i] = Sprite.Glow(r, _sparkColor[i]);

        // The bang: a big warm-white flash, plus a wider tinted glow that
        // lingers a moment, like the colored light the burst throws on the sky.
        _flash = Sprite.Glow(Math.Max(6, (int)(u * 0.13f)), Color.FromArgb(255, 240, 215));
        _haloPurple = Sprite.Glow(Math.Max(8, (int)(u * 0.24f)), Color.FromArgb(170, 80, 230));
        _haloGreen = Sprite.Glow(Math.Max(8, (int)(u * 0.24f)), Color.FromArgb(70, 230, 80));

        _rocketHead = Sprite.Glow(Math.Max(2, (int)(u * 0.008f)), Gold);
        _trailSpark = Sprite.Glow(Math.Max(2, (int)(u * 0.005f)), Gold);
    }

    public override void Begin(Random rng)
    {
        _rockets.Clear();
        int count = 2 + rng.Next(2);                                        // two or three
        float t0 = 0.2f;
        float u = _s.U;
        for (int n = 0; n < count; n++)
        {
            var rk = new Rocket
            {
                Launch = t0,
                Green = rng.Next(3) == 0,                                   // about one in three is the green one
                Reach = u * (0.16f + 0.06f * (float)rng.NextDouble()),
            };
            // Spread the rockets across the left and middle of the sky (the moon is on the right).
            float x = _s.Width * (0.10f + 0.46f * (n + (float)rng.NextDouble()) / count);
            float ridge = Brushwork.RidgeYAt(_s.FarRidge, x);
            rk.From = new PointF(x, ridge + u * 0.01f);                     // just under the hill's edge: it rises out from behind it
            float burstY = _s.Height * (0.14f + 0.26f * (float)rng.NextDouble());
            float drift = _s.Width * 0.03f * ((float)rng.NextDouble() * 2 - 1);
            rk.Burst = new PointF(x + drift, burstY);

            for (int i = 0; i < SparksPerRocket; i++)
            {
                float a = (float)(rng.NextDouble() * Math.PI * 2);
                rk.DirX[i] = MathF.Cos(a);
                rk.DirY[i] = MathF.Sin(a);
                // Most sparks ride the edge of the ball (that is what makes a
                // round burst), the rest fill the middle.
                rk.Speed[i] = rng.NextDouble() < 0.7 ? 0.8f + 0.2f * (float)rng.NextDouble() : 0.3f + 0.5f * (float)rng.NextDouble();
                rk.Phase[i] = (float)(rng.NextDouble() * Math.PI * 2);
                rk.Rate[i] = 22f + 26f * (float)rng.NextDouble();
                rk.Life[i] = 1.7f + 0.7f * (float)rng.NextDouble();           // between 1.7 and 2.4 s
                rk.Kind[i] = rk.Green
                    ? (rng.NextDouble() < 0.35 ? 3 : 2)                      // green with pale glitter
                    : (rng.NextDouble() < 0.5 ? 0 : 1);                      // purple and orange mixed
            }
            _rockets.Add(rk);
            t0 += 1.5f + 0.2f * (float)rng.NextDouble();
        }
        _seconds = _rockets[^1].Launch + Climb + 2.4f + 0.1f;               // last launch + climb + the longest spark life
    }

    /// <summary>A repeatable "random" number from 0 to 1 for any input: the same x always gives the same answer. Used for crackle, so Draw needs no stored state.</summary>
    private static float Hash(float x)
    {
        float v = MathF.Sin(x * 12.9898f) * 43758.547f;
        return v - MathF.Floor(v);
    }

    // Where the rocket is "age" seconds after launch: it slows as it rises
    // (an ease-out: fast at first, almost still at the top).
    private static PointF RocketAt(Rocket rk, float age)
    {
        float k = Math.Clamp(age / Climb, 0f, 1f);
        float e = 1 - MathF.Pow(1 - k, 2.2f);
        return new PointF(rk.From.X + (rk.Burst.X - rk.From.X) * e, rk.From.Y + (rk.Burst.Y - rk.From.Y) * e);
    }

    // Where one spark is "a" seconds after the bang: drag plus a gentle droop.
    private PointF SparkAt(Rocket rk, int i, float a)
    {
        float v = rk.Reach * Drag * rk.Speed[i];                    // launch speed, from the reach we want
        float dist = v * (1 - MathF.Exp(-Drag * a)) / Drag;
        float droop = _s.U * 0.012f * a * a;                        // gravity: grows with the square of age
        return new PointF(rk.Burst.X + rk.DirX[i] * dist, rk.Burst.Y + rk.DirY[i] * dist + droop);
    }

    public override void Draw(FrameBuffer fb, float t)
    {
        float u = _s.U;
        float lineW = Math.Max(1f, u * 0.0025f);

        foreach (Rocket rk in _rockets)
        {
            float age0 = t - rk.Launch;
            if (age0 < 0) continue;

            if (age0 < Climb)
            {
                // ---- Climbing: a thin trail of 12 line pieces, then a few sparkles, then the head ----
                const int pieces = 12;
                for (int j = 0; j < pieces; j++)
                {
                    float near = 1 - j / (float)pieces;                         // 1 at the head, fading to the tail
                    PointF a = RocketAt(rk, age0 - j * 0.03f);
                    PointF b = RocketAt(rk, age0 - (j + 1) * 0.03f);
                    fb.Line(a, b, Gold, 0.8f * near, Math.Max(1f, u * 0.003f * near), _s.OpenSky);
                }
                for (int j = 0; j < 6; j++)                                      // sparkles shed from the flame; each blinks on its own
                {
                    float sa = age0 - 0.05f - j * 0.055f;
                    if (sa < 0) continue;
                    PointF p = RocketAt(rk, sa);
                    float side = (Hash(j * 7.3f + rk.Launch * 11f) - 0.5f) * u * 0.012f;
                    float blink = Hash(MathF.Floor(t * 30f) + j * 3.1f);          // flickers about 30 times a second
                    _trailSpark.DrawCentered(fb, p.X + side, p.Y + u * 0.006f * j * 0.4f, blink * (1 - j / 7f), _s.OpenSky);
                }
                PointF head = RocketAt(rk, age0);
                _rocketHead.DrawCentered(fb, head.X, head.Y, 1f, _s.OpenSky);
                continue;
            }

            // ---- Burst ----
            float age = age0 - Climb;

            // The flash: instant and bright, gone in 0.15 s; the tinted halo hangs on for half a second.
            if (age < 0.15f)
                _flash.DrawCentered(fb, rk.Burst.X, rk.Burst.Y, 1f - age / 0.15f, _s.OpenSky);
            if (age < 0.55f)
                (rk.Green ? _haloGreen : _haloPurple).DrawCentered(fb, rk.Burst.X, rk.Burst.Y, 0.6f * (1 - Smooth(age / 0.55f)), _s.OpenSky);

            for (int i = 0; i < SparksPerRocket; i++)
            {
                float life = rk.Life[i];
                if (age > life) continue;

                // Fade: full for the first fifth of its life, then dims to nothing.
                float fade = 1 - Smooth((age - 0.3f * life) / (0.7f * life));

                // Crackle: in the last second the spark is "on" less and less often.
                float left = life - age;
                if (left < 1f)
                {
                    float c = 1 - left;                                          // 0 at one second left, 1 at the end
                    if (Hash(MathF.Floor(age * 24f) + rk.Phase[i] * 50f) < c * 0.7f) continue;
                }

                // Twinkle: brightness swings up and down on this spark's own rhythm.
                float twinkle = 0.55f + 0.45f * MathF.Sin(age * rk.Rate[i] + rk.Phase[i]);
                float op = fade * twinkle;
                if (op <= 0.02f) continue;

                PointF p = SparkAt(rk, i, age);
                // The streak is two pieces: a brighter short one next to the
                // spark, then a fainter, thinner one behind it (a comet's tail).
                PointF mid = SparkAt(rk, i, Math.Max(0f, age - StreakAge));
                PointF tail = SparkAt(rk, i, Math.Max(0f, age - 2.2f * StreakAge));
                fb.Line(mid, p, _sparkColor[rk.Kind[i]], 0.85f * op, lineW, _s.OpenSky);
                fb.Line(tail, mid, _sparkColor[rk.Kind[i]], 0.35f * op, Math.Max(1f, lineW * 0.6f), _s.OpenSky);
                _spark[rk.Kind[i]].DrawCentered(fb, p.X, p.Y, op, _s.OpenSky);
            }
        }
    }
}
