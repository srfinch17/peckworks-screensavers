using Peckworks.Screensavers.Core;

namespace Halloween.Happenings;

/// <summary>
/// Three to five will-o'-the-wisps: pale blue-green ghost lights that rise
/// from the tops of tombstones, wander lazily among the graves, pulse
/// softly, and one by one drift up and wink out. Folklore says they are
/// lost souls, or marsh gas catching fire. Either way they glow.
///
/// Each wisp has a PATH: a function that says where it is at any moment of
/// its own life. The path is three sine waves of different speeds added
/// together, once for across and once for up and down. One sine wave alone
/// swings back and forth like a pendulum and looks mechanical. Add a slower
/// one and a faster one, and the path curls and meanders and never quite
/// repeats, like a leaf on a pond.
///
/// The tail is free once there is a path function: a wisp's tail is just
/// the wisp as it was a moment ago. Ask the path "where were you 0.06
/// seconds ago, 0.12 seconds ago, ..." and stamp smaller, fainter lights
/// there. A trail of old positions, computed on the spot, no history kept.
/// </summary>
internal sealed class WillOWisps : Happening
{
    private const int MaxWisps = 5;
    private const int Tail = 7;
    private const int Sparkles = 4;

    private static readonly Color Ghostly = Color.FromArgb(120, 255, 220);

    /// <summary>Everything one wisp needs, rolled in Begin.</summary>
    private struct Wisp
    {
        public float Start, Life;                   // when it appears, and how long it lives (seconds)
        public PointF From;                         // the stone top it rises from
        public float Cx, Ax;                        // the middle of its wandering, and how far it can swing sideways
        public float[] Fx, Px, Fy, Py;              // frequencies and phases of the three waves, for x and for y
        public float[] SparkAngle, SparkSpin, SparkRadius, SparkPhase;
    }

    private readonly HalloweenScenery _s;
    private readonly Sprite _core, _halo, _spark;
    private readonly Sprite[] _tail = new Sprite[3];       // three sizes, big to small
    private readonly Wisp[] _wisps = new Wisp[MaxWisps];
    private int _count;
    private float _xMin, _xMax;                            // the stretch of graveyard they wander in

    public override float Seconds => 14f;

    public WillOWisps(HalloweenScenery s)
    {
        _s = s;
        _core = Sprite.Glow((int)Math.Max(3f, s.U * 0.02f), Ghostly);
        _halo = Sprite.Glow((int)Math.Max(6f, s.U * 0.045f), Color.FromArgb(90, 230, 210));
        _tail[0] = Sprite.Glow((int)Math.Max(3f, s.U * 0.0165f), Ghostly);
        _tail[1] = Sprite.Glow((int)Math.Max(2f, s.U * 0.0125f), Ghostly);
        _tail[2] = Sprite.Glow((int)Math.Max(2f, s.U * 0.009f), Ghostly);
        _spark = Sprite.Glow((int)Math.Max(2f, s.U * 0.005f), Color.FromArgb(220, 255, 245));

        _xMin = s.Tombstones.Min(t => t.Foot.X) - s.U * 0.02f;
        _xMax = s.Tombstones.Max(t => t.Foot.X) + s.U * 0.02f;
    }

    public override void Begin(Random rng)
    {
        float R() => (float)rng.NextDouble();
        _count = rng.Next(3, MaxWisps + 1);
        float span = _xMax - _xMin;
        for (int i = 0; i < _count; i++)
        {
            var stone = _s.Tombstones[rng.Next(_s.Tombstones.Count)];
            float ax = span * 0.25f;
            _wisps[i] = new Wisp
            {
                Start = i * 6.2f / _count + 0.4f * R(),           // staggered, so they appear one by one
                Life = 6f + R(),
                From = new PointF(stone.Foot.X, stone.Foot.Y - _s.TombstoneSize.Height * (stone.Cross ? 1.15f : 1f)),
                Ax = ax,
                Cx = _xMin + ax + R() * (span - 2 * ax),
                Fx = [0.4f + 0.3f * R(), 0.9f + 0.5f * R(), 1.7f + 0.7f * R()],
                Px = [R() * MathF.Tau, R() * MathF.Tau, R() * MathF.Tau],
                Fy = [0.5f + 0.3f * R(), 1.0f + 0.5f * R(), 1.9f + 0.7f * R()],
                Py = [R() * MathF.Tau, R() * MathF.Tau, R() * MathF.Tau],
                SparkAngle = Array.ConvertAll(new int[Sparkles], _ => R() * MathF.Tau),
                SparkSpin = Array.ConvertAll(new int[Sparkles], _ => (R() < 0.5f ? -1f : 1f) * (0.8f + R())),
                SparkRadius = Array.ConvertAll(new int[Sparkles], _ => 0.012f + 0.014f * R()),
                SparkPhase = Array.ConvertAll(new int[Sparkles], _ => R() * MathF.Tau),
            };
        }
    }

    /// <summary>
    /// Where the wisp is "u" seconds into its own life. First it climbs from
    /// the stone into its wandering (blended in over 1.6 s), then it
    /// meanders, then for its last 1.8 s it rises away.
    /// </summary>
    private PointF PositionAt(in Wisp w, float u)
    {
        float us = _s.U;
        // Across: three waves that add up to at most Ax, so it stays among the stones.
        float x = w.Cx + w.Ax * (0.6f * MathF.Sin(w.Fx[0] * u + w.Px[0]) + 0.3f * MathF.Sin(w.Fx[1] * u + w.Px[1])
                               + 0.1f * MathF.Sin(w.Fx[2] * u + w.Px[2]));
        // Up and down: a middle height 0.075 U above the ground there, swinging 0.05 U either way
        // (so between 0.025 and 0.125 U above it).
        float y = _s.Ground.YAt(x) - us * 0.075f
                  - us * 0.05f * (0.6f * MathF.Sin(w.Fy[0] * u + w.Py[0]) + 0.3f * MathF.Sin(w.Fy[1] * u + w.Py[1])
                                + 0.1f * MathF.Sin(w.Fy[2] * u + w.Py[2]));
        float m = Smooth(u / 1.6f);                            // 0 = at the stone, 1 = wandering
        x = w.From.X + (x - w.From.X) * m;
        y = w.From.Y + (y - w.From.Y) * m;
        y -= us * 0.18f * Smooth((u - (w.Life - 1.8f)) / 1.8f); // the last drift upward
        return new PointF(x, y);
    }

    public override void Draw(FrameBuffer fb, float t)
    {
        float us = _s.U;
        for (int i = 0; i < _count; i++)
        {
            Wisp w = _wisps[i];
            float u = t - w.Start;
            if (u < 0 || u > w.Life) continue;

            // Brightness: fades up over 0.8 s, then a quick wink out over the final 1.2 s.
            float on = Smooth(u / 0.8f) * Smooth((w.Life - u) / 1.2f);
            float pulse = 0.82f + 0.12f * MathF.Sin(u * 2.7f + i) + 0.06f * MathF.Sin(u * 7f + 2 * i);

            // The tail, oldest first so the newest lies on top. Where the wisp was k * 0.06 s ago.
            for (int k = Tail; k >= 1; k--)
            {
                float uk = u - 0.06f * k;
                if (uk < 0) continue;
                PointF p = PositionAt(w, uk);
                float fade = MathF.Pow(1 - k / (Tail + 1f), 1.3f);
                _tail[k <= 2 ? 0 : k <= 5 ? 1 : 2].DrawCentered(fb, p.X, p.Y, on * pulse * 0.55f * fade);
            }

            PointF at = PositionAt(w, u);
            _halo.DrawCentered(fb, at.X, at.Y, on * pulse * 0.6f);
            _core.DrawCentered(fb, at.X, at.Y, on * pulse);

            // Sparkles: little crosses of light that circle the wisp at their own pace and twinkle.
            // A sine wave cubed is mostly near zero with brief bright peaks, which is exactly a twinkle.
            for (int j = 0; j < Sparkles; j++)
            {
                float twinkle = MathF.Pow(Math.Max(0f, MathF.Sin(u * 5f + w.SparkPhase[j])), 3f) * on;
                if (twinkle < 0.05f) continue;
                float a = w.SparkAngle[j] + w.SparkSpin[j] * u;
                float rad = w.SparkRadius[j] * us;
                var c = new PointF(at.X + MathF.Cos(a) * rad, at.Y + MathF.Sin(a) * rad * 0.8f);
                float len = Math.Max(2f, us * 0.005f) * (0.6f + 0.6f * twinkle);
                fb.Line(new PointF(c.X - len, c.Y), new PointF(c.X + len, c.Y), Color.FromArgb(200, 255, 235), twinkle * 0.7f, 1f);
                fb.Line(new PointF(c.X, c.Y - len), new PointF(c.X, c.Y + len), Color.FromArgb(200, 255, 235), twinkle * 0.7f, 1f);
                _spark.DrawCentered(fb, c.X, c.Y, twinkle);
            }
        }
    }
}
