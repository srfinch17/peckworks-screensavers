using Peckworks.Screensavers.Core;

namespace SakuraPond;

/// <summary>
/// The water's edge, alive: where the pond meets the sand the water laps, a
/// few pixels up the shore and back, slowly, along the whole bank at once but
/// not all in step (a swell travels along it), and every animal can ask where
/// the water is RIGHT NOW.
///
/// FEYNMAN VERSION. Think of the shore as a ruler laid across the water's
/// edge at every point along the bank: zero at the edge, minus up the sand,
/// plus out into the water. The painter paints the pond with the edge at
/// zero. Each frame the water's edge sits at a different mark on the ruler,
/// "e", between minus a bit and plus a bit: a pixel whose mark is beyond e
/// is under water this frame, one before it is dry. So the live picture of the
/// shore is nothing more than "for every pixel near the edge, which side of e
/// is it on", and writing the wet or the dry colour of that pixel. Both colours
/// were worked out once, when the shore was built: the wet colour of a sand
/// pixel is the shallowest water painted over it by the painter's own recipe,
/// the dry colour of a water pixel is the bed it would show when the water
/// pulls back (wet sand, darker). Only the pixels whose state differs from the
/// painted picture are written (the backdrop copy already put the rest down),
/// and at the edge itself a soft mix over a few pixels plus a pale thread
/// where the water's skin meets the ground, so the edge is never a hard line.
///
/// The edge is not a smooth curve either: a fixed wobble along the bank is
/// baked into the picture at rest, so where the painter's curve was smooth the
/// water now runs a little further up here and a little less there, as it does
/// on real sand.
///
/// HOW A WAVE LAPS (2026-10-26). Watch a pond's edge: the water runs UP the
/// sand quickly and drains back slowly, and the running edge carries a thin
/// line of foam that the draining edge does not. So the swell is not a plain
/// sine: each rhythm is a sine with its crest pushed forward (a sawtooth with
/// the corners rounded off), and the pale thread at the edge is brightest
/// while the edge is advancing. And a ring on the water (a koi's gulp, a frog's
/// splash) that reaches the bank washes up it: the ripples say how hard they
/// are pushing at each part of the bank, and that is added to the swell there.
///
/// STONES. A stone at the water's edge has its own waterline: the water meets
/// the stone's flank round its rim, not along the bank's curve (the painter's
/// WrapWater darkens that wet flank once). The band here includes the water
/// side of every stone's rim, measured in from the rim, so when the swell runs
/// up the sand it runs up the stone's foot as well, darkening it, and leaves
/// it damp as it drains. Lily pads are left out: they float.
///
/// Cost: the band is about forty pixels wide along the bank; a few tens of
/// thousands of entries, each a compare and sometimes one pixel write. The
/// swell moves under a quarter of a pixel a frame, so nothing flickers.
/// </summary>
internal sealed class Shoreline
{
    // The band, as arrays side by side (one entry per pixel near the edge):
    private readonly int[] _idx;        // the pixel's index in the frame
    private readonly float[] _s;        // its mark on the ruler, in pixels: minus up the sand (or in from a stone's rim), plus into the water, wobble included
    private readonly byte[] _t;         // where along the bank it is, 0 to 255 (which part of the swell it feels)
    private readonly uint[] _dry, _wet; // its two colours
    private readonly uint[] _damp;      // the dry colour darkened: wet sand (or wet stone) the water has just left
    private readonly bool[] _pictureWet;// what the backdrop shows there: true = the wet colour is already down (a wet frame writes nothing), false = the dry one
    private readonly bool[] _rock;      // a stone's foot (no shade under the edge, foam only on a run-up)
    private readonly float[] _wetness;  // 1 the moment the water covers it, drying toward 0 over a few seconds after it leaves
    // The same marks looked up by pixel, for the animals' questions (short.MinValue = not in the band).
    private readonly short[] _sMap;
    private readonly byte[] _tMap;
    private readonly bool[] _inPond;    // the painter's water (for pixels outside the band)
    private readonly int _w, _h;
    private readonly float _reach;      // how far the edge moves each way at most, in pixels
    private readonly float[] _edge = new float[256];   // this frame's e along the bank
    private readonly float[] _adv = new float[256];    // how briskly the edge is running up the sand there, 0 to 1 (foam)
    private readonly float[] _surge = new float[256];  // the rings' push at each part of the bank this frame (Ripples.Surge)
    private readonly PointF[] _at = new PointF[256];   // where each part of the bank is on the screen (for the ripples)
    private readonly float _ph1, _ph2, _ph3;           // the swell's phases (from the bank's shape, no dice)
    private readonly float _surgeBand;
    private double _time;
    private float _dryStep;
    private readonly string? _log = Environment.GetEnvironmentVariable("PECKWORKS_SHORELOG");

    /// <summary>How many times an animal has backed off from a wave (an instrument; see Flinch).</summary>
    public int Flinches { get; private set; }

    /// <summary>How many pixels the band holds (a placement is counted, never assumed).</summary>
    public int Count => _idx.Length;

    /// <summary>How far the water comes up the sand past the painted edge at most, in pixels (a ring's wash can add half as much again).</summary>
    public float Reach => _reach;

    /// <param name="px">The finished backdrop (shaded); the rest state of the shore is baked into it here.</param>
    /// <param name="inPond">The painter's water.</param>
    /// <param name="waterDist">Distance from the bank, water side, in tenths of a pixel (the painter's transform).</param>
    /// <param name="landDist">Distance from the water, land side, in tenths of a pixel.</param>
    /// <param name="blocked">White where a stone, a cap or the log stands: its water-side rim joins the band, the rest of it is left alone.</param>
    /// <param name="padded">White under the lily pads: never painted.</param>
    /// <param name="rockIn">Inside a stone: how far in from its rim, tenths of a pixel.</param>
    /// <param name="bank">The bank curve, from the screen's side round to its bottom.</param>
    public Shoreline(uint[] px, bool[] inPond, int[] waterDist, int[] landDist, uint[] blocked, uint[] padded, int[] rockIn, PointF[] bank, int w, int h, float u)
    {
        _w = w; _h = h; _inPond = inPond;
        _reach = u * 0.013f;                         // (0.011 until 2026-10-26)
        _surgeBand = u * 0.03f;
        float wobble = u * 0.004f;
        float half = _reach * 1.5f + wobble + 3f;    // the band up the sand: as far as the edge can go (a wash adds half a reach), plus the soft edge
        float halfWater = _reach + wobble + 3f;      // and out into the water: a wash only ever ADVANCES the edge, so the water side needs no allowance for it (a third of the band's cost, 1440p)
        _ph1 = bank[3].X * 0.013f; _ph2 = bank[7].Y * 0.011f; _ph3 = bank[^1].X * 0.007f;

        // The bank as 128 evenly spaced points, so "where along the bank" is smooth.
        var fine = new PointF[128];
        for (int k = 0; k < fine.Length; k++)
        {
            float f = k / (fine.Length - 1f) * (bank.Length - 1);
            int i = Math.Min(bank.Length - 2, (int)f);
            float r = f - i;
            fine[k] = new PointF(bank[i].X + (bank[i + 1].X - bank[i].X) * r, bank[i].Y + (bank[i + 1].Y - bank[i].Y) * r);
        }
        for (int k = 0; k < _at.Length; k++)
        {
            float f = k / 255f * (fine.Length - 1);
            int i = Math.Min(fine.Length - 2, (int)f);
            float r = f - i;
            _at[k] = new PointF(fine[i].X + (fine[i + 1].X - fine[i].X) * r, fine[i].Y + (fine[i + 1].Y - fine[i].Y) * r);
        }
        float Wobble(float t) => wobble * (0.62f * MathF.Sin(MathF.Tau * 11 * t + _ph1) + 0.38f * MathF.Sin(MathF.Tau * 23 * t + _ph2));

        var idx = new List<int>(); var s = new List<float>(); var t = new List<byte>(); var dry = new List<uint>(); var wet = new List<uint>();
        var pictureWet = new List<bool>(); var rock = new List<bool>();
        _sMap = new short[w * h]; _tMap = new byte[w * h];
        Array.Fill(_sMap, short.MinValue);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                if ((padded[i] & 0xFF) >= 128) continue;
                bool stone = (blocked[i] & 0xFF) >= 128;
                float raw;
                if (stone)
                {
                    // A stone's foot: only its water side, measured in from its own rim, no wobble (the rim is the wobble).
                    if (!inPond[i]) continue;
                    raw = -rockIn[i] / 10f;
                    if (raw < -half) continue;
                }
                else
                {
                    raw = inPond[i] ? waterDist[i] / 10f : -landDist[i] / 10f;
                    if (raw > halfWater || raw < -half) continue;
                }
                // Which part of the bank is nearest: a coarse search over the fine points (once, at build).
                int best = 0; float bd = float.MaxValue;
                for (int k = 0; k < fine.Length; k++)
                {
                    float dx = fine[k].X - x, dy = fine[k].Y - y, d = dx * dx + dy * dy;
                    if (d < bd) { bd = d; best = k; }
                }
                float tt = best / (fine.Length - 1f);
                float mark = stone ? raw : raw + Wobble(tt);
                uint c = px[i];
                int r = (int)((c >> 16) & 0xFF), g = (int)((c >> 8) & 0xFF), b = (int)(c & 0xFF);
                uint dryC, wetC;
                if (stone)
                {
                    // Dry: the stone as painted (its wet flank included). Wet: the stone under a skin of water, darker and bluer.
                    dryC = c;
                    wetC = Pack(r * 0.52f, g * 0.56f, b * 0.68f);
                }
                else if (inPond[i])
                {
                    // The bed the water leaves when it pulls back: wet sand, darker than the painted shallows.
                    wetC = c;
                    dryC = Pack(r + (84 - r) * 0.6f, g + (76 - g) * 0.6f, b + (60 - b) * 0.6f);
                }
                else
                {
                    // Sand under the shallowest water, by the painter's own recipe (PourWater at depth 0):
                    // the ground tinted green and seen through 85 percent, the water's own colour the rest, a touch of sky.
                    dryC = c;
                    float tr = r * 0.62f, tg = g * 0.8f, tb = b * 0.74f;
                    float wr = 86 + (tr - 86) * 0.85f, wg = 140 + (tg - 140) * 0.85f, wb = 118 + (tb - 118) * 0.85f;
                    wetC = Pack(wr + (200 - wr) * 0.04f, wg + (218 - wg) * 0.04f, wb + (228 - wb) * 0.04f);
                }
                idx.Add(i); s.Add(mark); t.Add((byte)(tt * 255)); dry.Add(dryC); wet.Add(wetC); rock.Add(stone);
                _sMap[i] = (short)Math.Clamp(mark * 10, short.MinValue + 1, short.MaxValue);
                _tMap[i] = (byte)(tt * 255);
                // The rest state, wobble included, goes into the picture: where the wobble puts the edge further up
                // the sand that sand is painted wet, where it pulls it back that water is painted as bed. A stone's
                // foot stays as painted (its mark is never above zero).
                if (!stone && mark > 0 && !inPond[i]) px[i] = wetC;
                else if (!stone && mark <= 0 && inPond[i]) px[i] = dryC;
                pictureWet.Add(!stone && mark > 0);
            }
        _idx = idx.ToArray(); _s = s.ToArray(); _t = t.ToArray(); _dry = dry.ToArray(); _wet = wet.ToArray();
        _pictureWet = pictureWet.ToArray(); _rock = rock.ToArray();
        // Wet sand is darker and a touch cooler than dry: the colour the water leaves behind, drying back over seconds.
        // A stone's foot dries from its wet colour.
        _damp = new uint[_dry.Length];
        for (int n = 0; n < _damp.Length; n++)
        {
            uint c = _dry[n];
            _damp[n] = _rock[n] ? _wet[n] : Pack(((c >> 16) & 0xFF) * 0.58f, ((c >> 8) & 0xFF) * 0.62f, (c & 0xFF) * 0.68f);
        }
        _wetness = new float[_dry.Length];
    }

    private static uint Pack(float r, float g, float b) =>
        (uint)Math.Clamp((int)r, 0, 255) << 16 | (uint)Math.Clamp((int)g, 0, 255) << 8 | (uint)Math.Clamp((int)b, 0, 255);

    /// <summary>
    /// A sine with its crest pushed forward: the fall (the water running up
    /// the sand, e going down) is quick and the rise (the drain) is slow.
    /// "k" is how lopsided, 0 a plain sine.
    /// </summary>
    private static float Lap(double theta, float k) => (float)Math.Sin(theta - k * Math.Sin(theta));

    /// <summary>Advances the swell. Three slow rhythms, so the edge never repeats itself exactly; and the rings' wash.</summary>
    /// <param name="ripples">The pond's rings: one reaching the bank washes up it (null: none).</param>
    public void Update(float dt, Ripples? ripples = null)
    {
        _time += dt;
        _dryStep = dt / 6f;                 // wet sand dries back to pale in about six seconds
        if (ripples != null) ripples.Surge(_at, _surge, _surgeBand); else Array.Clear(_surge);
        float fullAdv = _reach * 1.6f;      // an edge running up the sand this fast (pixels a second) carries full foam
        for (int k = 0; k < _edge.Length; k++)
        {
            float t = k / 255f;
            float e = _reach * (0.5f * Lap(MathF.Tau * (3 * t - _time / 4.5) + _ph1, 0.55f)
                              + 0.3f * Lap(MathF.Tau * (7 * t + _time / 2.9) + _ph2, 0.45f)
                              + 0.2f * (float)Math.Sin(MathF.Tau * (_time / 7.0) + _ph3))
                    - _reach * 0.6f * _surge[k];
            float vel = dt > 1e-4f ? (e - _edge[k]) / dt : 0;              // minus = running up the sand
            _adv[k] += (Math.Clamp(-vel / fullAdv, 0, 1) - _adv[k]) * MathF.Min(1, dt * 6);   // the foam follows the run-up with a short lag, and lingers a moment
            _edge[k] = e;
        }
    }

    /// <summary>Paints the shore as it is this frame, right after the backdrop copy (the lawn's blades overhang it after).</summary>
    public void Draw(FrameBuffer fb)
    {
        uint[] px = fb.Pixels;
        int[] idx = _idx; float[] s = _s; byte[] t = _t; uint[] dry = _dry, wet = _wet, damp = _damp; float[] edge = _edge, wetness = _wetness, adv = _adv;
        bool[] pictureWet = _pictureWet, rock = _rock;
        float dryStep = _dryStep;
        for (int n = 0; n < idx.Length; n++)
        {
            float mark = s[n];
            int k = t[n];
            float d = mark - edge[k];                 // plus: under water this frame
            if (d >= 2.5f)
            {
                wetness[n] = 1;
                if (!pictureWet[n]) px[idx[n]] = wet[n];                      // wet now; the picture has it dry
                continue;
            }
            // Out of the water, or at its edge: the ground, darker for as long as it is still wet from the last swell.
            float wn = wetness[n];
            if (wn > 0) { wn = MathF.Max(0, wn - dryStep); wetness[n] = wn; }
            uint ground = wn > 0.01f ? Mix(dry[n], damp[n], wn * 0.85f) : dry[n];
            if (d <= -2.5f)
            {
                if (pictureWet[n] || wn > 0.01f) px[idx[n]] = ground;         // dry now; the picture has it wet, or it is still damp
                continue;
            }
            // The edge itself: a soft mix over five pixels, a pale thread where the water's skin meets the
            // ground (foam: brightest while the edge is running up the sand; on a stone only then), and the
            // water a shade darker just inside it (the ground's shadow under the surface).
            float a = (d + 2.5f) / 5f;
            uint c = Mix(ground, wet[n], a);
            float m = 1 - MathF.Abs(d) / 2.5f;
            float foam = rock[n] ? 0.4f * adv[k] : 0.24f + 0.24f * adv[k];
            c = FrameBuffer.Blend(c, 232, 240, 234, foam * m);
            if (d > 0 && !rock[n]) c = FrameBuffer.Blend(c, 10, 30, 32, 0.12f * (d / 2.5f));
            px[idx[n]] = c;
        }
    }

    private static uint Mix(uint a, uint b, float k)
    {
        int ka = (int)(k * 256), kb = 256 - ka;
        uint r = (((a >> 16) & 0xFF) * (uint)kb + ((b >> 16) & 0xFF) * (uint)ka) >> 8;
        uint g = (((a >> 8) & 0xFF) * (uint)kb + ((b >> 8) & 0xFF) * (uint)ka) >> 8;
        uint bl = ((a & 0xFF) * (uint)kb + (b & 0xFF) * (uint)ka) >> 8;
        return r << 16 | g << 8 | bl;
    }

    /// <summary>Is this point under water RIGHT NOW (the lapping edge counted)? Off the picture: no.</summary>
    public bool WetNow(float x, float y)
    {
        int ix = (int)x, iy = (int)y;
        if (ix < 0 || iy < 0 || ix >= _w || iy >= _h) return false;
        int i = iy * _w + ix;
        short s = _sMap[i];
        if (s == short.MinValue) return _inPond[i];
        return s / 10f > _edge[_tMap[i]];
    }

    /// <summary>Can the water ever reach this point (a place to stand, sit or drink should be beyond it)?</summary>
    public bool EverWet(float x, float y)
    {
        int ix = (int)x, iy = (int)y;
        if (ix < 0 || iy < 0 || ix >= _w || iy >= _h) return false;
        int i = iy * _w + ix;
        short s = _sMap[i];
        if (s == short.MinValue) return _inPond[i];
        return s / 10f > -_reach * 1.5f - 2.5f;
    }

    /// <summary>Can the water ever leave this pixel bare (a thing that must stay under water never settles here)?</summary>
    public bool EverDry(int i)
    {
        short s = _sMap[i];
        if (s == short.MinValue) return !_inPond[i];
        return s / 10f < _reach + 2.5f;
    }

    /// <summary>An animal backed off from a wave: counted, and written to PECKWORKS_SHORELOG with the second if that names a file.</summary>
    public void Flinch(string who)
    {
        Flinches++;
        if (!string.IsNullOrEmpty(_log)) File.AppendAllText(_log, $"{_time:F1} {who}\r\n");
    }
}
