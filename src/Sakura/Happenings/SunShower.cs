using Peckworks.Screensavers.Core;

namespace Sakura.Happenings;

/// <summary>
/// A sun shower: rain that falls while the sun is shining. In Japan it is
/// called "kitsune no yomeiri", the fox's wedding, and it is a happy sign.
///
/// Two parts. First, a few hundred fine, slanted streaks of rain that catch
/// the light (pale silver, see-through), some of which glint now and then.
/// Second, on the lake, little rings that pop where drops land.
///
/// Both work from t alone. A streak's place is "where it started plus how far
/// it has fallen, wrapped": when it drops off the bottom it comes back in at
/// the top, like a fairground carousel. A ripple slot is a little clock that
/// restarts every second or so; each restart picks a new landing spot from a
/// scrambled version of the slot number (a "hash"), so the spots look random
/// but are the same every time we ask about the same moment.
/// </summary>
internal sealed class SunShower : Happening
{
    private const int StreakCount = 200;
    private const int RippleSlots = 30;
    private const float Slant = 0.18f;          // sideways pixels per pixel fallen

    private readonly Scenery _s;
    private readonly Sprite _glint;

    // One entry per streak, rolled in Begin.
    private readonly float[] _x0 = new float[StreakCount], _y0 = new float[StreakCount];
    private readonly float[] _speed = new float[StreakCount], _len = new float[StreakCount], _alpha = new float[StreakCount];
    private readonly float[] _glintPhase = new float[StreakCount];   // < 0 means "this streak never glints"
    private uint _seed;

    public override float Seconds => 12f;
    public override int Layer => 1;
    public override string? Claims => "weather";

    public SunShower(Scenery s)
    {
        _s = s;
        _glint = Sprite.Glow(Math.Max(3, (int)(s.U * 0.006f)), Color.FromArgb(255, 255, 255));
    }

    public override void Begin(Random rng)
    {
        float u = _s.U;
        _seed = (uint)rng.Next();
        for (int i = 0; i < StreakCount; i++)
        {
            _x0[i] = (float)rng.NextDouble() * (_s.Width + Slant * _s.Height) - Slant * _s.Height;
            _y0[i] = (float)rng.NextDouble() * _s.Height * 2;
            _speed[i] = (0.8f + 0.4f * (float)rng.NextDouble());
            _len[i] = u * (0.02f + 0.03f * (float)rng.NextDouble());
            _alpha[i] = 0.25f + 0.2f * (float)rng.NextDouble();
            _glintPhase[i] = rng.NextDouble() < 0.28 ? (float)(rng.NextDouble() * 6.283) : -1f;
        }
    }

    /// <summary>A cheap scrambler: the same two numbers always give the same 0..1 answer.</summary>
    private static float Hash(uint a, uint b, uint c)
    {
        uint h = a * 374761393u + b * 668265263u + c * 2246822519u;
        h = (h ^ (h >> 13)) * 1274126177u;
        h ^= h >> 16;
        return (h & 0xFFFFFF) / (float)0x1000000;
    }

    public override void Draw(FrameBuffer fb, float t)
    {
        float fade = Fade(t, Seconds, 2f, 3f);                      // the shower comes up over 2 s and dies away over 3 s
        if (fade < 0.01f) return;
        float u = _s.U;
        int w = fb.Width, h = fb.Height;
        var silver = Color.FromArgb(236, 244, 252);
        float thick = Math.Max(1f, u * 0.0008f);
        float fall = u * 1.2f * t;                                  // 1.2 screen heights per second
        float span = h + 2 * u * 0.05f;                             // the loop: the screen plus room for a streak to leave and re-enter

        // ---- ripples on the lake (under the rain) ----
        float lakeTop = _s.HorizonY + u * 0.02f;
        var open = _s.OpenBehindBanks;
        for (int i = 0; i < RippleSlots; i++)
        {
            float period = 0.9f + 0.5f * Hash((uint)i, 1, _seed);
            float clock = t + period * Hash((uint)i, 2, _seed);
            uint cycle = (uint)(clock / period);
            float age = (clock - cycle * period) / 0.6f;            // 0 to 1 over the ripple's 0.6 s life; more than 1 = resting
            if (age >= 1f) continue;
            float x = w * (0.01f + 0.98f * Hash((uint)i, cycle, _seed));
            float depth = Hash((uint)i, cycle + 7919, _seed);
            float y = lakeTop + (h - lakeTop) * MathF.Pow(depth, 0.8f);
            int ix = (int)x, iy = (int)y;
            if (ix < 0 || ix >= w || iy < 0 || iy >= h || iy <= _s.HorizonY || !open[iy * w + ix]) continue;
            float near = 0.45f + 0.9f * (y - _s.HorizonY) / Math.Max(1f, h - _s.HorizonY);   // nearer water: bigger rings
            float a = (1f - age) * 0.5f * fade;
            Ring(fb, x, y, Math.Max(1.5f, u * 0.014f * near * age), a, thick, open);
            if (age > 0.2f)
                Ring(fb, x, y, Math.Max(1f, u * 0.014f * near * (age - 0.2f) * 1.25f), a * 0.7f, thick, open);
        }

        // ---- the rain ----
        for (int i = 0; i < StreakCount; i++)
        {
            float travel = (_y0[i] + fall * _speed[i]) % span;
            float hy = travel - u * 0.05f;                          // the streak's lower end (its head)
            float hx = _x0[i] + Slant * hy;
            float len = _len[i];
            float dy = len / MathF.Sqrt(1 + Slant * Slant), dx = Slant * dy;
            fb.Line(new PointF(hx - dx, hy - dy), new PointF(hx, hy), silver, _alpha[i] * fade, thick);

            if (_glintPhase[i] >= 0)
            {
                // A tiny star of light that pulses on and off: sharp pulses (raised to a high power).
                float pulse = MathF.Pow(Math.Max(0f, (float)Math.Sin(t * 3.1 + _glintPhase[i])), 10);
                if (pulse > 0.05f) _glint.DrawCentered(fb, hx, hy, pulse * 0.9f * fade);
            }
        }
    }

    /// <summary>A flattened ring (a circle seen at a slant): a short chain of tiny lines.</summary>
    private static void Ring(FrameBuffer fb, float cx, float cy, float r, float alpha, float thick, bool[] open)
    {
        const int Steps = 10;
        var color = Color.FromArgb(240, 248, 255);
        float rx = r, ry = r * 0.35f;
        PointF prev = new(cx + rx, cy);
        for (int k = 1; k <= Steps; k++)
        {
            double ang = k * Math.PI * 2 / Steps;
            var p = new PointF(cx + rx * (float)Math.Cos(ang), cy + ry * (float)Math.Sin(ang));
            fb.Line(prev, p, color, alpha, thick, open);
            prev = p;
        }
    }
}
