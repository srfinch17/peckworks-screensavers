using Peckworks.Screensavers.Core;

namespace SakuraPond;

/// <summary>
/// Sunlight on the water (2026-10-30, his ask: "some light shimmering on the water"): THE GLINTS. Sparkle on the
/// surface: a few hundred sites on sunlit open water, each a short horizontal dash (a ripple crest seen from above at
/// a slant is wider than it is tall) in the sky's colour, blinking on and off on its own short clock, drifting with
/// the breeze. The breeze is a slow field: sheets of glitter here and there, calm elsewhere, more toward the far side
/// where the surface is seen at more of a slant. None in the boughs' shade (Pond.Sun), none under a lily pad (live
/// centres, read each frame), under the floating petals. Light is solid to nothing.
///
/// THE BEAMS AND THE POOLS ARE GONE (2026-11-02, "the whole light ray thing is a disaster either fix it or remove
/// it"). From 2026-10-30 to then a shaft of light went down from each hole in the boughs' shade, bent by Snell's law
/// and baked into the backdrop, and landed as a warm pool of light on the bed, stamped each frame. Measured with the
/// knob at 0 against 100 on the same seed, the beams and the pools together moved under 1.5 percent of the pixels by
/// more than six grey levels (the glints are most of that): a shaft of light seen from ABOVE the water is nothing, it
/// is seen from the side or from under the water, and what he was calling light rays was the bed's own slope lighting.
/// What an overhead view really shows of sun in water is caustics (bright wavy nets on a shallow bed) and the glitter
/// on the surface; the glitter stays, the caustics are a round of their own if he wants them.
///
/// Dice: its own Random (nothing else in the scene moves when it is added). Knob "Sunlight" (percent): 0 is off, 100
/// is this, 200 twice the glints. The glint site count in the start log and PECKWORKS_SUNLOG are the "is it on
/// screen at all" instrument.
/// </summary>
internal sealed class Sunlight
{
    private readonly Pond _pond;
    private readonly float _strength;
    private readonly Random _rng;
    private readonly int _w, _h;
    private readonly float _u;
    private Glint[] _glints = [];
    private readonly Sprite[] _dash = new Sprite[3];
    private float _t;                       // seconds, for the log only
    private float _breezeA, _breezeB;       // the breeze field's two phases
    private readonly StreamWriter? _log;
    private int _frame;

    private struct Glint
    {
        public float X, Y;          // where it is, screen pixels (drifts)
        public float Phase, Rate;   // its own clock: on when sin is high
        public float Strength;      // how bright its brightest is
        public byte Size;           // which dash sprite
        public float Sun;           // how much sun reaches the surface there (Pond.Sun, 0 to 1)
    }

    public Sunlight(Pond pond, float strength, Random rng)
    {
        _pond = pond; _strength = strength; _rng = rng;
        _w = pond.Width; _h = pond.Height; _u = pond.U;
        string? file = Environment.GetEnvironmentVariable("PECKWORKS_SUNLOG");
        if (!string.IsNullOrEmpty(file)) { _log = new StreamWriter(file, append: false); _log.WriteLine("# t,glintsOn,meanOpacity"); }
        for (int k = 0; k < 3; k++) _dash[k] = Dash(Math.Max(4, (int)(_u * (0.006f + 0.0035f * k))));   // 6 to 13 px wide at 1080 lines (4 to 10 was specks nobody saw)
    }

    /// <summary>A glint: a short horizontal dash, soft, in the sky's colour, w pixels wide and about a third as tall.</summary>
    private static Sprite Dash(int w)
    {
        int h = Math.Max(2, w / 3 + 1);
        var px = new uint[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float fx = (x + 0.5f) / w * 2 - 1, fy = (y + 0.5f) / h * 2 - 1;
                float d = fx * fx + fy * fy;
                float a = d >= 1 ? 0 : MathF.Sqrt(1 - d);                                 // a crisp little bar with a soft last pixel: squared it was a smudge nobody saw
                px[y * w + x] = (uint)((int)(a * 255) << 24 | 236 << 16 | 246 << 8 | 252);
            }
        return Sprite.FromPixels(w, h, px);
    }

    /// <summary>Scatters the glint sites on sunlit open water (after the boughs have said where the water is in view). Count scales with the knob.</summary>
    public void PlaceGlints(bool[] waterInView)
    {
        if (_strength <= 0) return;
        int want = (int)(380 * _strength * (_w * (float)_h) / (_u * _u * 16f / 9f));   // 380 per 16:9 screen's worth of pond at 100 percent
        var list = new List<Glint>(want);
        for (int tries = 0; tries < want * 40 && list.Count < want; tries++)
        {
            int x = _rng.Next(_w), y = _rng.Next(_h), i = y * _w + x;
            if (!waterInView[i] || _pond.Sun[i] < 200) continue;
            float far = 0.45f + 0.55f * (1 - y / (float)_h);                            // more toward the far side
            if (_rng.NextDouble() > far) continue;
            list.Add(new Glint
            {
                X = x, Y = y,
                Phase = (float)(_rng.NextDouble() * Math.Tau), Rate = 3.5f + 4.5f * (float)_rng.NextDouble(),
                Strength = 0.35f + 0.65f * (float)_rng.NextDouble(),
                Size = (byte)_rng.Next(3), Sun = _pond.Sun[i] / 255f,
            });
        }
        _glints = list.ToArray();
    }

    /// <summary>How many glint sites there are, for the start log.</summary>
    public int GlintSites => _glints.Length;

    public void Update(float dt)
    {
        _t += dt;
        // Every rhythm keeps its own phase, wrapped (a sine of one long clock jumps the day the clock wraps).
        const float Tau = (float)Math.Tau;
        _breezeA = (_breezeA + 0.6f * dt) % Tau; _breezeB = (_breezeB + 0.45f * dt) % Tau;
        // The glints drift with the breeze, slowly, and come round again from the other side of the pond.
        float driftX = 0.004f * _u * dt, driftY = 0.0015f * _u * dt;
        for (int k = 0; k < _glints.Length; k++)
        {
            ref Glint g = ref _glints[k];
            g.Phase = (g.Phase + g.Rate * dt) % Tau;
            g.X += driftX; g.Y += driftY;
            if (g.X >= _w - 1) g.X -= _u * 0.3f;
            if (g.Y >= _h - 1) g.Y -= _u * 0.2f;
        }
    }

    /// <summary>The glints on the surface: over the ripples and the pads, under the floating petals.</summary>
    public void DrawGlints(FrameBuffer fb, bool[] onlyWhere)
    {
        int on = 0; float sum = 0;
        IReadOnlyList<LilyPad> pads = _pond.Pads;
        for (int k = 0; k < _glints.Length; k++)
        {
            ref Glint g = ref _glints[k];
            // Its own clock: on for the top of the sine only, so each glint blinks briefly and is dark most of the time.
            float s = MathF.Sin(g.Phase);
            if (s < 0.55f) continue;
            // The breeze: sheets of glitter that drift across the pond; dead calm elsewhere.
            float wind = 0.5f + 0.5f * MathF.Sin(g.X / (_u * 0.42f) + _breezeA + 0.8f) * MathF.Sin(g.Y / (_u * 0.3f) - _breezeB + 2.0f);
            wind = wind * wind;
            float a = (s - 0.55f) / 0.45f;
            float opacity = MathF.Min(1f, 2.4f * a * g.Strength * g.Sun * (0.25f + 0.75f * wind) * MathF.Min(1f, _strength));
            if (opacity < 0.05f) continue;
            bool underPad = false;
            for (int p = 0; p < pads.Count && !underPad; p++)
            {
                LilyPad pad = pads[p];
                float dx = g.X - pad.Center.X, dy = (g.Y - pad.Center.Y) / 0.82f;
                underPad = dx * dx + dy * dy <= pad.Radius * pad.Radius * 1.1f;
            }
            if (underPad) continue;
            _dash[g.Size].DrawCentered(fb, g.X, g.Y, opacity, onlyWhere);
            on++; sum += opacity;
        }
        if (_log != null && ++_frame % 30 == 0)
        {
            _log.WriteLine(FormattableString.Invariant($"{_t:F2},{on},{(on > 0 ? sum / on : 0):F3}"));
            _log.Flush();
        }
    }
}
