using Peckworks.Screensavers.Core;

namespace SakuraPond;

/// <summary>
/// Sunlight on and in the water (2026-10-30, his ask: "little patches of light rays that illuminate the pond all
/// the way through the water to the bottom and account for the water's effect on the light", and "some light
/// shimmering on the water"). Three things, all light, solid to nothing, drawn only on open water:
///
///   1. THE BEAMS. Where the sun gets through a hole in the cherry boughs' shade, a shaft of light goes down into
///      the water. Light entering water bends toward the vertical (Snell: sin 36.9 / 1.33 = 0.45, so the beam runs
///      at 26.8 degrees, 0.30 right and 0.40 down the ground per unit of depth where the shadow above water runs
///      0.45 and 0.6 per unit of height, Light.ShadowX and ShadowY), dims as it goes down (water absorbs it: exp of
///      minus five times the depth, so the shaft is a short bright wedge under the surface, not a pillar to the
///      bottom), and what the eye sees of it is veiled by the water above it (Light.Murk at that depth, the fish's
///      own veil). Each point of it is drawn sunk down the screen by its depth (Light.SinkPx), like everything in the
///      water. The beams never move (the sun does not), so they are BAKED into the backdrop once (Bake): no cost per
///      frame. The fish swim over them, lit by nothing: a fish IN a beam would be brighter; not done.
///   2. THE POOLS. Where a beam lands on the bed it makes a pool of light, as bright as the light that got down there
///      (the same absorption) and as visible as the bed is at that depth (the painter's BedClear), warm in colour. A
///      pool is a Sprite stamped each frame on the bed (under every swimmer, after the backdrop), drifting a few
///      pixels and breathing a little, which is what light refracted through a moving surface does; it is hidden
///      where a stone is nearer the eye (Pond.Near, the depth test) and never painted where the lapping shore may
///      paint (Shoreline.EverDry), which redraws its own band each frame.
///   3. THE GLINTS. Sparkle on the surface: a few hundred sites on sunlit open water, each a short horizontal dash
///      (a ripple crest seen from above at a slant is wider than it is tall) in the sky's colour, blinking on and
///      off on its own short clock, drifting with the breeze. The breeze is a slow field: sheets of glitter here and
///      there, calm elsewhere, more toward the far side where the surface is seen at more of a slant. None in the
///      boughs' shade (Pond.Sun), none under a lily pad (live centres, read each frame), under the floating petals.
///
/// Dice: its own Random (nothing else in the scene moves when it is added). Knob "Sunlight" (percent): 0 is off, 100
/// is this, 200 twice the glints and brighter beams. Counts for the start log and PECKWORKS_SUNLOG are the "is it
/// on screen at all" instrument.
/// </summary>
internal sealed class Sunlight
{
    private readonly Pond _pond;
    private readonly float _strength;
    private readonly Random _rng;
    private readonly int _w, _h;
    private readonly float _u;
    private readonly List<(Sprite sprite, float cx, float cy, int nearness, float phase)> _pools = new();
    private Glint[] _glints = [];
    private readonly Sprite[] _dash = new Sprite[3];
    private float _t;                       // seconds, for the log only
    private float _breezeA, _breezeB;       // the breeze field's two phases
    private float _wobA, _wobB, _wobC, _wobD;   // the pools' four wobble rhythms
    private readonly StreamWriter? _log;
    private int _frame;

    /// <summary>How many holes in the shade were found, for the start log.</summary>
    public int Gaps { get; private set; }

    /// <summary>Where they are, and each pool's box and depth, for the start log (a reader crops the picture by these).</summary>
    public string Where { get; private set; } = "";

    private struct Glint
    {
        public float X, Y;          // where it is, screen pixels (drifts)
        public float Phase, Rate;   // its own clock: on when sin is high
        public float Strength;      // how bright its brightest is
        public byte Size;           // which dash sprite
        public float Sun;           // how much sun reaches the surface there (Pond.Sun, 0 to 1)
    }

    /// <summary>Finds the gaps and bakes the beams into the backdrop (so call it BEFORE the boughs bake their still parts over it) and builds the pools.</summary>
    public Sunlight(Pond pond, float strength, Random rng)
    {
        _pond = pond; _strength = strength; _rng = rng;
        _w = pond.Width; _h = pond.Height; _u = pond.U;
        string? file = Environment.GetEnvironmentVariable("PECKWORKS_SUNLOG");
        if (!string.IsNullOrEmpty(file)) { _log = new StreamWriter(file, append: false); _log.WriteLine("# t,glintsOn,meanOpacity,pools"); }
        for (int k = 0; k < 3; k++) _dash[k] = Dash(Math.Max(4, (int)(_u * (0.006f + 0.0035f * k))));   // 6 to 13 px wide at 1080 lines (4 to 10 was specks nobody saw)
        if (strength > 0) Bake();
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

    private void Bake()
    {
        int w = _w, h = _h; float u = _u;
        byte[] sun = _pond.Sun;
        // ---- the holes in the shade: full sun inside a patch that is mostly shaded ----
        float[] sunBlur = Blur(sun, w, h, Math.Max(2, (int)(u * 0.06f)));
        var gapness = new float[w * h];
        for (int i = 0; i < gapness.Length; i++)
            if (_pond.Water[i] && sun[i] >= 250 && sunBlur[i] <= 225) gapness[i] = 250 - sunBlur[i];
        float gapR = u * 0.045f;
        var gaps = new List<(int cx, int cy)>();
        for (int g = 0; g < 7; g++)
        {
            int best = -1; float bestV = 0;
            for (int i = 0; i < gapness.Length; i++) if (gapness[i] > bestV) { bestV = gapness[i]; best = i; }
            if (best < 0) break;
            int cx = best % w, cy = best / w;
            gaps.Add((cx, cy));
            int r3 = (int)(gapR * 3);
            for (int y = Math.Max(0, cy - r3); y <= Math.Min(h - 1, cy + r3); y++)
                for (int x = Math.Max(0, cx - r3); x <= Math.Min(w - 1, cx + r3); x++)
                    gapness[y * w + x] = 0;                                              // no second gap within three radii
        }
        Gaps = gaps.Count;
        var where = new System.Text.StringBuilder();
        foreach (var (cx, cy) in gaps) where.Append(FormattableString.Invariant($" gap {cx},{cy}"));
        // ---- the beams, as a lift map over the screen, and each gap's pool on the bed ----
        float D = Pond.DepthU * u;
        const float rx = 0.45f / 0.75f * 0.505f, ry = 0.6f / 0.75f * 0.505f;           // the beam's run along the ground per unit of depth, bent by the water
        var shaft = new float[w * h];
        var foot = new bool[w * h];
        const int steps = 90;
        foreach (var (cx, cy) in gaps)
        {
            Array.Clear(foot);
            int x0 = Math.Max(0, cx - (int)gapR), x1 = Math.Min(w - 1, cx + (int)gapR), y0 = Math.Max(0, cy - (int)gapR), y1 = Math.Min(h - 1, cy + (int)gapR);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float dx = x - cx, dy = y - cy;
                    int i = y * w + x;
                    if (dx * dx + dy * dy <= gapR * gapR && _pond.Water[i] && sun[i] >= 235) foot[i] = true;   // the real shape of the hole
                }
            var pool = new float[w * h];
            int px0 = w, px1 = -1, py0 = h, py1 = -1;
            float poolZ = 0; int poolN = 0;
            for (int k = 0; k <= steps; k++)
            {
                float z = k / (float)steps;
                float gx = z * D * rx, gy = z * D * ry, sink = Light.SinkPx(z, u);
                float bright = MathF.Exp(-5f * z) * (1 - Light.Murk(z));
                for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                    {
                        if (!foot[y * w + x]) continue;
                        int gxi = (int)(x + gx), gyi = (int)(y + gy);
                        if (gxi < 0 || gyi < 0 || gxi >= w || gyi >= h || !_pond.Water[gyi * w + gxi]) continue;
                        float bed = _pond.Depth[gyi * w + gxi] / 255f;
                        if (z > bed) continue;                                            // the beam has landed
                        int sy = (int)(y + gy + sink);
                        if (sy >= h) continue;
                        int j = sy * w + gxi;
                        shaft[j] += bright * 0.02f;
                        if (z + 1f / steps > bed)
                        {
                            pool[j] += MathF.Exp(-1.2f * bed) * (1 - Light.Murk(bed)) * (0.4f + 0.48f * (1 - bed));
                            poolZ += bed; poolN++;
                            px0 = Math.Min(px0, gxi); px1 = Math.Max(px1, gxi); py0 = Math.Min(py0, sy); py1 = Math.Max(py1, sy);
                        }
                    }
            }
            if (poolN == 0) continue;
            // The pool as a sprite: its lift map blurred (softer the deeper), warm, alpha from the lift; never where the
            // shore's own band paints, and only the box it fills.
            int soft = Math.Max(2, (int)(u * (0.006f + 0.01f * poolZ / poolN)));
            px0 = Math.Max(0, px0 - soft * 3); px1 = Math.Min(w - 1, px1 + soft * 3); py0 = Math.Max(0, py0 - soft * 3); py1 = Math.Min(h - 1, py1 + soft * 3);
            int bw = px1 - px0 + 1, bh = py1 - py0 + 1;
            if (bw <= 0 || bh <= 0) continue;
            var box = new float[bw * bh];
            for (int y = py0; y <= py1; y++) for (int x = px0; x <= px1; x++) box[(y - py0) * bw + (x - px0)] = pool[y * w + x];
            float[] poolSoft = Blur(box, bw, bh, soft);                                  // the box alone: whole-frame blurs per pool cost most of a second at the start
            var argb = new uint[bw * bh];
            for (int y = py0; y <= py1; y++)
                for (int x = px0; x <= px1; x++)
                {
                    int i = y * w + x;
                    if (!_pond.Water[i] || _pond.Shore.EverDry(i)) continue;
                    int a = (int)(Math.Clamp(poolSoft[(y - py0) * bw + (x - px0)] * 2.5f * MathF.Min(1, _strength), 0, 0.85f) * 255);
                    if (a == 0) continue;
                    argb[(y - py0) * bw + (x - px0)] = (uint)(a << 24 | 255 << 16 | 240 << 8 | 190);
                }
            _pools.Add((Sprite.FromPixels(bw, bh, argb), px0 + bw / 2f, py0 + bh / 2f, Light.Nearness(poolZ / poolN), (float)(_rng.NextDouble() * Math.Tau)));
            where.Append(FormattableString.Invariant($" pool {px0},{py0},{bw}x{bh},z{poolZ / poolN:F2}"));
        }
        Where = where.ToString();
        // ---- the beams into the backdrop: screened toward warm sunlight; what is bright already climbs less ----
        float[] shaftSoft = Blur(shaft, w, h, Math.Max(2, (int)(u * 0.008f)));
        uint[] px = _pond.Pixels;
        for (int i = 0; i < px.Length; i++)
        {
            if (!_pond.Water[i]) continue;
            float lift = Math.Clamp(shaftSoft[i] * 3f * MathF.Min(1.5f, _strength), 0, 1);
            if (lift <= 0.002f) continue;
            uint c = px[i];
            int r = (int)((c >> 16) & 0xFF), g = (int)((c >> 8) & 0xFF), b = (int)(c & 0xFF);
            r += (int)((255 - r) * lift); g += (int)((255 - g) * lift * 0.92f); b += (int)((255 - b) * lift * 0.7f);
            px[i] = (uint)(Math.Min(255, r) << 16 | Math.Min(255, g) << 8 | Math.Min(255, b));
        }
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
        _wobA = (_wobA + 0.9f * dt) % Tau; _wobB = (_wobB + 0.55f * dt) % Tau; _wobC = (_wobC + 0.75f * dt) % Tau; _wobD = (_wobD + 1.53f * dt) % Tau;
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

    /// <summary>The pools of light on the bed: after the backdrop and the shore, under every swimmer.</summary>
    public void DrawPools(FrameBuffer fb)
    {
        foreach (var (sprite, cx, cy, nearness, phase) in _pools)
        {
            // Refracted through a moving surface, the pool drifts a few pixels round its place and breathes a little.
            float dx = MathF.Sin(_wobA + phase) * _u * 0.004f + MathF.Sin(_wobB + phase + 1.3f) * _u * 0.002f;
            float dy = MathF.Cos(_wobC + phase) * _u * 0.003f;
            float breath = 0.86f + 0.14f * MathF.Sin(_wobD + phase + 0.5f);
            sprite.DrawCentered(fb, cx + dx, cy + dy, breath, depth: _pond.Near, nearness: nearness);
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
            _log.WriteLine(FormattableString.Invariant($"{_t:F2},{on},{(on > 0 ? sum / on : 0):F3},{_pools.Count}"));
            _log.Flush();
        }
    }

    /// <summary>A byte picture smoothed by a box blur of radius r, run twice (near enough a soft bell), as floats.</summary>
    private static float[] Blur(byte[] src, int w, int h, int r) { var a = new float[src.Length]; for (int i = 0; i < a.Length; i++) a[i] = src[i]; return Blur(a, w, h, r); }

    private static float[] Blur(float[] src, int w, int h, int r)
    {
        var a = (float[])src.Clone(); var b = new float[a.Length];
        for (int pass = 0; pass < 2; pass++)
        {
            for (int y = 0; y < h; y++)
            {
                int row = y * w; float sum = 0; int n = 0;
                for (int x = 0; x < Math.Min(w, r); x++) { sum += a[row + x]; n++; }
                for (int x = 0; x < w; x++)
                {
                    if (x + r < w) { sum += a[row + x + r]; n++; }
                    if (x - r - 1 >= 0) { sum -= a[row + x - r - 1]; n--; }
                    b[row + x] = sum / n;
                }
            }
            for (int x = 0; x < w; x++)
            {
                float sum = 0; int n = 0;
                for (int y = 0; y < Math.Min(h, r); y++) { sum += b[y * w + x]; n++; }
                for (int y = 0; y < h; y++)
                {
                    if (y + r < h) { sum += b[(y + r) * w + x]; n++; }
                    if (y - r - 1 >= 0) { sum -= b[(y - r - 1) * w + x]; n--; }
                    a[y * w + x] = sum / n;
                }
            }
        }
        return a;
    }
}
