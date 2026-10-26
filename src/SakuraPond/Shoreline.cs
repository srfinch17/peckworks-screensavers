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
/// Rocks are left out: the waterline on a stone runs round the stone's own rim
/// (PondPainter.WrapWater) and does not move. Lily pads are left out too.
///
/// Cost: the band is about thirty pixels wide along the bank; a few tens of
/// thousands of entries, each a compare and sometimes one pixel write. The
/// swell moves under a quarter of a pixel a frame, so nothing flickers.
/// </summary>
internal sealed class Shoreline
{
    // The band, as arrays side by side (one entry per pixel near the edge):
    private readonly int[] _idx;        // the pixel's index in the frame
    private readonly float[] _s;        // its mark on the ruler, in pixels: minus up the sand, plus into the water, wobble included
    private readonly byte[] _t;         // where along the bank it is, 0 to 255 (which part of the swell it feels)
    private readonly uint[] _dry, _wet; // its two colours
    private readonly uint[] _damp;      // the dry colour darkened: wet sand the water has just left
    private readonly float[] _wetness;  // 1 the moment the water covers it, drying toward 0 over a few seconds after it leaves
    // The same marks looked up by pixel, for the animals' questions (short.MinValue = not in the band).
    private readonly short[] _sMap;
    private readonly byte[] _tMap;
    private readonly bool[] _inPond;    // the painter's water (for pixels outside the band)
    private readonly int _w, _h;
    private readonly float _reach;      // how far the edge moves each way at most, in pixels
    private readonly float[] _edge = new float[256];   // this frame's e along the bank
    private readonly float _ph1, _ph2, _ph3;           // the swell's phases (from the bank's shape, no dice)
    private double _time;
    private float _dryStep;
    private readonly string? _log = Environment.GetEnvironmentVariable("PECKWORKS_SHORELOG");

    /// <summary>How many times an animal has backed off from a wave (an instrument; see Flinch).</summary>
    public int Flinches { get; private set; }

    /// <summary>How many pixels the band holds (a placement is counted, never assumed).</summary>
    public int Count => _idx.Length;

    /// <summary>How far the water comes up the sand past the painted edge at most, in pixels.</summary>
    public float Reach => _reach;

    /// <param name="px">The finished backdrop (shaded); the rest state of the shore is baked into it here.</param>
    /// <param name="inPond">The painter's water.</param>
    /// <param name="waterDist">Distance from the bank, water side, in tenths of a pixel (the painter's transform).</param>
    /// <param name="landDist">Distance from the water, land side, in tenths of a pixel.</param>
    /// <param name="skip">White where the band must not paint (rocks, the log, the caps, lily pads).</param>
    /// <param name="bank">The bank curve, from the screen's side round to its bottom.</param>
    public Shoreline(uint[] px, bool[] inPond, int[] waterDist, int[] landDist, uint[] skip, PointF[] bank, int w, int h, float u)
    {
        _w = w; _h = h; _inPond = inPond;
        _reach = u * 0.011f;
        float wobble = u * 0.004f;
        float half = _reach + wobble + 3f;           // the band: as far as the edge can go, plus the soft edge
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
        float Wobble(float t) => wobble * (0.62f * MathF.Sin(MathF.Tau * 11 * t + _ph1) + 0.38f * MathF.Sin(MathF.Tau * 23 * t + _ph2));

        var idx = new List<int>(); var s = new List<float>(); var t = new List<byte>(); var dry = new List<uint>(); var wet = new List<uint>();
        _sMap = new short[w * h]; _tMap = new byte[w * h];
        Array.Fill(_sMap, short.MinValue);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                float raw = inPond[i] ? waterDist[i] / 10f : -landDist[i] / 10f;
                if (MathF.Abs(raw) > half || (skip[i] & 0xFF) >= 128) continue;
                // Which part of the bank is nearest: a coarse search over the fine points (once, at build).
                int best = 0; float bd = float.MaxValue;
                for (int k = 0; k < fine.Length; k++)
                {
                    float dx = fine[k].X - x, dy = fine[k].Y - y, d = dx * dx + dy * dy;
                    if (d < bd) { bd = d; best = k; }
                }
                float tt = best / (fine.Length - 1f);
                float mark = raw + Wobble(tt);
                uint c = px[i];
                int r = (int)((c >> 16) & 0xFF), g = (int)((c >> 8) & 0xFF), b = (int)(c & 0xFF);
                uint dryC, wetC;
                if (inPond[i])
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
                idx.Add(i); s.Add(mark); t.Add((byte)(tt * 255)); dry.Add(dryC); wet.Add(wetC);
                _sMap[i] = (short)Math.Clamp(mark * 10, short.MinValue + 1, short.MaxValue);
                _tMap[i] = (byte)(tt * 255);
                // The rest state, wobble included, goes into the picture: where the wobble puts the edge further up
                // the sand that sand is painted wet, where it pulls it back that water is painted as bed.
                if (mark > 0 && !inPond[i]) px[i] = wetC;
                else if (mark <= 0 && inPond[i]) px[i] = dryC;
            }
        _idx = idx.ToArray(); _s = s.ToArray(); _t = t.ToArray(); _dry = dry.ToArray(); _wet = wet.ToArray();
        // Wet sand is darker and a touch cooler than dry: the colour the water leaves behind, drying back over seconds.
        _damp = new uint[_dry.Length];
        for (int n = 0; n < _damp.Length; n++)
        {
            uint c = _dry[n];
            _damp[n] = Pack(((c >> 16) & 0xFF) * 0.58f, ((c >> 8) & 0xFF) * 0.62f, (c & 0xFF) * 0.68f);
        }
        _wetness = new float[_dry.Length];
    }

    private static uint Pack(float r, float g, float b) =>
        (uint)Math.Clamp((int)r, 0, 255) << 16 | (uint)Math.Clamp((int)g, 0, 255) << 8 | (uint)Math.Clamp((int)b, 0, 255);

    /// <summary>Advances the swell. Three slow rhythms, so the edge never repeats itself exactly.</summary>
    public void Update(float dt)
    {
        _time += dt;
        _dryStep = dt / 6f;                 // wet sand dries back to pale in about six seconds
        for (int k = 0; k < _edge.Length; k++)
        {
            float t = k / 255f;
            _edge[k] = _reach * (0.5f * (float)Math.Sin(MathF.Tau * (3 * t - _time / 4.5) + _ph1)
                              + 0.3f * (float)Math.Sin(MathF.Tau * (7 * t + _time / 2.9) + _ph2)
                              + 0.2f * (float)Math.Sin(MathF.Tau * (_time / 7.0) + _ph3));
        }
    }

    /// <summary>Paints the shore as it is this frame, right after the backdrop copy (the lawn's blades overhang it after).</summary>
    public void Draw(FrameBuffer fb)
    {
        uint[] px = fb.Pixels;
        int[] idx = _idx; float[] s = _s; byte[] t = _t; uint[] dry = _dry, wet = _wet, damp = _damp; float[] edge = _edge, wetness = _wetness;
        float dryStep = _dryStep;
        for (int n = 0; n < idx.Length; n++)
        {
            float mark = s[n];
            float d = mark - edge[t[n]];              // plus: under water this frame
            if (d >= 2.5f)
            {
                wetness[n] = 1;
                if (mark <= 0) px[idx[n]] = wet[n];                          // wet now; the picture has it dry
                continue;
            }
            // Out of the water, or at its edge: the ground, darker for as long as it is still wet from the last swell.
            float wn = wetness[n];
            if (wn > 0) { wn = MathF.Max(0, wn - dryStep); wetness[n] = wn; }
            uint ground = wn > 0.01f ? Mix(dry[n], damp[n], wn * 0.85f) : dry[n];
            if (d <= -2.5f)
            {
                if (mark > 0 || wn > 0.01f) px[idx[n]] = ground;             // dry now; the picture has it wet, or it is still damp
                continue;
            }
            // The edge itself: a soft mix over five pixels, a pale thread where the water's skin meets the
            // ground, and the water a shade darker just inside it (the ground's shadow under the surface).
            float a = (d + 2.5f) / 5f;
            uint c = Mix(ground, wet[n], a);
            float m = 1 - MathF.Abs(d) / 2.5f;
            c = FrameBuffer.Blend(c, 232, 240, 234, 0.32f * m);
            if (d > 0) c = FrameBuffer.Blend(c, 10, 30, 32, 0.12f * (d / 2.5f));
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
        return s / 10f > -_reach - 2.5f;
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
