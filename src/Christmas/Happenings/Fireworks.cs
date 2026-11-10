using Peckworks.Screensavers.Core;
using Peckworks.Screensavers.Core.Sakura;

namespace Christmas.Happenings;

/// <summary>
/// A CHRISTMAS FIREWORKS SHOW over the valley: about 27 seconds with a shape.
///
///   opening   three single shells, one at a time
///   middle    a snowflake, a pair, the CHRISTMAS TREE (alone, so it can be read), then three overlapping bursts
///   the beat  two quiet seconds while the last smoke drifts
///   finale    ten shells in about three seconds, then one big gold willow
///
/// Each run rolls different shells, places and heights (all in Begin), but always that arc.
///
/// EVERY SHELL IS A WHOLE LIFE, and the whole life is worked out from t alone (no state is kept from frame
/// to frame; "closed form" means: give me the time, I give you the place, no stepping needed):
///   1. LAUNCH: a faint trail climbs from the village in the valley with a slight wobble, slowing as it rises
///      (it is going up against gravity, like a ball thrown up). A spark rides at its head.
///   2. FLASH: a tiny bright pop at the top. A faint coloured glow lights the sky, the mountains and the
///      valley for about half a second, and a ghost of smoke is left that drifts with the breeze.
///   3. BURST: each star has a final spot, a vector from the burst centre, rolled once in Begin. Its distance
///      along that vector at age a is  reach * (1 - exp(-k a)).  That is AIR DRAG: it starts at speed
///      reach*k and keeps slowing, creeping to its final spot, the way a paper plane coasts to a stop. That
///      is why a burst "hangs": most of the travel is in the first third of a second, then the stars barely
///      creep. Gravity is a fall that also meets drag: it speeds up and settles to a steady sinking speed
///      (a leaf falling): drop(a) = vt * (a - (1 - exp(-1.3 a)) / 1.3).
///   4. Each star leaves a short fading tail (the same formula asked a moment ago), twinkles, and its
///      brightness eases to zero at the end of its own life, so nothing pops off.
///
/// THE SHAPES (a flat picture of a burst): a ball is points on a sphere seen from the side (so they bunch
/// at the rim, like a real ball of stars); a ring is points on a circle squashed by a tilt; the star, the
/// snowflake and the tree are lists of points that keep their shape because drag shrinks every distance by
/// the same fraction (a drawing on a balloon keeps its shape while the balloon blows up).
///
/// COST: stars are small soft dots stamped from pre-painted sprites (four sub-pixel positions each way, so
/// a hanging star glides instead of stepping a whole pixel). Tails are one to four short lines. The big
/// soft glows (the lighting and the smoke) are limited to the strongest few per frame, and the live star
/// count is thinned smoothly (the faintest first) when too many shells are alive at once. Nothing is
/// allocated in Draw.
/// </summary>
internal sealed class Fireworks : Happening
{
    private enum Kind { Peony, Willow, Chrys, Ring, Double, Star, Snow, Tree, Cross, Pastel }

    private const int MaxShells = 26, MaxStars = 124, MaxPieces = 5;
    private const float LiveBudget = 850f;          // about this many bright stars at once; beyond it the faintest thin out
    private const int Red = 0, Green = 1, Gold = 2, White = 3, Ice = 4, Pink = 5, Orange = 6, Violet = 7, Silver = 8;

    private static readonly Color[] Pal =
    [
        Color.FromArgb(255, 60, 55),     // red
        Color.FromArgb(70, 240, 105),    // green
        Color.FromArgb(255, 200, 90),    // gold
        Color.FromArgb(245, 248, 255),   // white
        Color.FromArgb(140, 205, 255),   // ice blue
        Color.FromArgb(255, 160, 205),   // pastel pink
        Color.FromArgb(255, 140, 60),    // warm orange
        Color.FromArgb(185, 150, 255),   // pastel violet
        Color.FromArgb(205, 218, 235),   // silver
    ];

    private readonly ChristmasScenery _s;
    private readonly int _r0, _r1;                                  // dot radii in pixels: small, main
    private readonly Sprite[][] _dot = new Sprite[2][];             // [size][colour * 16 + subpixel cell]
    private readonly Sprite[] _flare = new Sprite[9];               // the early bright halo of a star, per colour
    private readonly Sprite[][] _amb = new Sprite[2][];             // the sky-lighting glow, two sizes, per colour
    private readonly Sprite[] _smoke = new Sprite[2];
    private readonly Sprite _flash, _skyFlash;
    private readonly Sprite[] _wash = new Sprite[9];           // a wide flat glow for the snowfield, per colour
    private readonly Sprite[] _dotT = new Sprite[16];          // the tree's bigger green star, four sub-pixel cells each way
    private readonly bool[] _snow;                             // stencil: true on the hill (the snowfield), false on everything above its crest
    private readonly int _rT;
    private readonly int _ambR0, _ambR1;

    // ---- the plan (rolled in Begin) ----
    private int _n;
    private float _seconds = 27f;
    private readonly byte[] _kind = new byte[MaxShells];
    private readonly float[] _tl = new float[MaxShells], _tb = new float[MaxShells], _lift = new float[MaxShells];
    private readonly float[] _x0 = new float[MaxShells], _y0 = new float[MaxShells], _bx = new float[MaxShells], _by = new float[MaxShells];
    private readonly float[] _R = new float[MaxShells], _ph = new float[MaxShells];
    private readonly float[] _drag = new float[MaxShells], _vt = new float[MaxShells], _life = new float[MaxShells], _hold = new float[MaxShells];
    private readonly float[] _tau = new float[MaxShells], _tc = new float[MaxShells], _spin = new float[MaxShells];
    private readonly float[] _fall = new float[MaxShells], _fallAt = new float[MaxShells];
    private readonly float[] _sdx = new float[MaxShells], _sdy = new float[MaxShells];
    private readonly int[] _pieces = new int[MaxShells], _count = new int[MaxShells];
    private readonly bool[] _big = new bool[MaxShells], _fin = new bool[MaxShells], _skyF = new bool[MaxShells];
    private readonly float[] _keep = new float[MaxShells];
    private float _windX, _windY;

    // per star (shell * MaxStars + j)
    private readonly float[] _ox = new float[MaxShells * MaxStars], _oy = new float[MaxShells * MaxStars];
    private readonly float[] _sph = new float[MaxShells * MaxStars], _rate = new float[MaxShells * MaxStars];
    private readonly float[] _lf = new float[MaxShells * MaxStars], _gm = new float[MaxShells * MaxStars], _rank = new float[MaxShells * MaxStars];
    private readonly byte[] _col = new byte[MaxShells * MaxStars], _flag = new byte[MaxShells * MaxStars];   // flag: 1 crackle, 2 big, 4 leaf

    // per frame scratch (no allocation in Draw)
    private readonly float[] _amS = new float[MaxShells], _smS = new float[MaxShells], _wsS = new float[MaxShells];
    private readonly float[] _top = new float[8];
    private readonly float[] _E = new float[MaxPieces + 1], _C = new float[MaxPieces + 1], _S = new float[MaxPieces + 1], _Dd = new float[MaxPieces + 1];

    private static readonly bool NoWash = Environment.GetEnvironmentVariable("PECKWORKS_NOWASH") != null;   // testing: look at the snow with and without the wash
    private Random _rng = new(1);

    public override float Seconds => _seconds;
    public override string? Claims => "skyshow,costly";

    // ====================================================================================================
    //  Painting the sprites (constructor: paints only, rolls nothing)
    // ====================================================================================================

    public Fireworks(ChristmasScenery s)
    {
        _s = s;
        float U = s.U;
        _r0 = Math.Max(1, (int)MathF.Round(U * 0.0026f));
        _r1 = Math.Max(2, (int)MathF.Round(U * 0.0056f));
        int r2 = Math.Max(3, (int)MathF.Round(U * 0.0085f));
        _dot[0] = new Sprite[9 * 16];
        _dot[1] = new Sprite[9 * 16];
        for (int c = 0; c < 9; c++)
        {
            for (int q = 0; q < 16; q++)
            {
                float fx = (q & 3) / 4f, fy = (q >> 2) / 4f;
                _dot[0][c * 16 + q] = MakeDot(_r0, Pal[c], fx, fy);
                _dot[1][c * 16 + q] = MakeDot(_r1, Pal[c], fx, fy);
            }
            _flare[c] = MakeDot(r2, Pal[c], 0, 0);
        }
        _ambR0 = Math.Max(8, (int)(U * 0.085f));
        _ambR1 = Math.Max(10, (int)(U * 0.17f));
        _amb[0] = new Sprite[9];
        _amb[1] = new Sprite[9];
        for (int c = 0; c < 9; c++)
        {
            _amb[0][c] = MakeSoft(_ambR0, Pal[c], 2.2f, 1f, -1);
            _amb[1][c] = MakeSoft(_ambR1, Pal[c], 2.2f, 1f, -1);
        }
        _smoke[0] = MakeSoft(Math.Max(8, (int)(U * 0.075f)), Color.FromArgb(125, 135, 170), 1.6f, 1f, 11);
        _smoke[1] = MakeSoft(Math.Max(10, (int)(U * 0.115f)), Color.FromArgb(125, 135, 170), 1.6f, 1f, 23);
        _flash = MakeSoft(Math.Max(6, (int)(U * 0.06f)), Color.FromArgb(255, 244, 225), 1.8f, 1f, -1);

        // The whole-sky flash of the biggest shells: one very large glow, used very faintly for a few frames.
        _skyFlash = MakeSoft(Math.Max(20, (int)(U * 0.30f)), Color.FromArgb(255, 236, 205), 2.0f, 1f, -1);

        // The wash of colour a burst throws on the snow: a wide, flat ellipse (the snow lies flat), one per colour.
        for (int c = 0; c < 9; c++) _wash[c] = MakeSoft(Math.Max(10, (int)(U * 0.30f)), Math.Max(6, (int)(U * 0.12f)), Pal[c], 1.8f, 1f, -1);

        // The tree's own stars are half again as big as the others.
        _rT = Math.Max(3, (int)MathF.Round(U * 0.0085f));
        for (int q = 0; q < 16; q++) _dotT[q] = MakeDot(_rT, Pal[Green], (q & 3) / 4f, (q >> 2) / 4f);

        // The snow stencil: below the hill's crest line, in each column. Read-only use of the scenery.
        _snow = new bool[s.Width * s.Height];
        for (int x = 0; x < s.Width; x++)
        {
            int y0 = Math.Clamp((int)s.Ground.YAt(x) + 2, 0, s.Height);
            for (int y = y0; y < s.Height; y++) _snow[y * s.Width + x] = true;
        }
    }

    /// <summary>
    /// A small soft dot: a bright near-white middle fading to the star's colour and then to clear. It is
    /// painted with its centre (offX, offY) pixels off the pixel grid, so stamping the variant nearest to a
    /// star's true position puts it within a quarter of a pixel of where it really is.
    /// </summary>
    private static Sprite MakeDot(int r, Color c, float offX, float offY)
    {
        int S = 2 * r + 2;
        var px = new uint[S * S];
        float cx = r + 1 + offX, cy = r + 1 + offY, rad = r + 0.7f;
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float d = MathF.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy)) / rad;
                if (d >= 1f) continue;
                float a = MathF.Pow(1f - d, 1.15f);
                float hot = MathF.Max(0f, 1f - d / 0.5f); hot *= hot * 0.85f;          // white-hot in the middle only
                int cr = (int)(c.R + (255 - c.R) * hot), cg = (int)(c.G + (255 - c.G) * hot), cb = (int)(c.B + (255 - c.B) * hot);
                px[y * S + x] = ((uint)(a * 255) << 24) | ((uint)cr << 16) | ((uint)cg << 8) | (uint)cb;
            }
        return Sprite.FromPixels(S, S, px);
    }

    /// <summary>
    /// A big soft glow, one colour, fading as (1 - distance)^power. The falloff is looked up from a short
    /// table by the SQUARE of the distance, so no square root per pixel. noiseSeed >= 0 multiplies it by
    /// a slow blotchy pattern, which turns a round blob into a wisp of smoke.
    /// </summary>
    private static Sprite MakeSoft(int r, Color c, float power, float gain, int noiseSeed) => MakeSoft(r, r, c, power, gain, noiseSeed);

    private static Sprite MakeSoft(int rx, int ry, Color c, float power, float gain, int noiseSeed)
    {
        int Sx = 2 * rx, Sy = 2 * ry;
        var lut = new float[1024];
        for (int i = 0; i < 1024; i++) lut[i] = 255f * gain * MathF.Pow(1f - MathF.Sqrt(i / 1023f), power);
        var px = new uint[Sx * Sy];
        for (int y = 0; y < Sy; y++)
            for (int x = 0; x < Sx; x++)
            {
                float dx = (x + 0.5f - rx) / rx, dy = (y + 0.5f - ry) / ry;
                float dd = dx * dx + dy * dy;
                if (dd >= 1f) continue;
                float a = lut[(int)(dd * 1023f)];
                if (noiseSeed >= 0)
                {
                    float n = 0.5f * Noise(x * 4f / Sx, y * 4f / Sx, noiseSeed) + 0.3f * Noise(x * 9f / Sx, y * 9f / Sx, noiseSeed + 5) + 0.2f * Noise(x * 19f / Sx, y * 19f / Sx, noiseSeed + 9);
                    a *= Math.Clamp((n - 0.2f) * 1.6f, 0f, 1f);
                }
                px[y * Sx + x] = ((uint)Math.Clamp((int)a, 0, 255) << 24) | ((uint)c.R << 16) | ((uint)c.G << 8) | c.B;
            }
        return Sprite.FromPixels(Sx, Sy, px);
    }

    private static float Hash(int x, int y, int seed)
    {
        uint h = (uint)(x * 374761393 + y * 668265263 + seed * 2147483647);
        h = (h ^ (h >> 13)) * 1274126177u;
        return ((h ^ (h >> 16)) & 0xFFFF) / 65535f;
    }

    /// <summary>Smooth value noise: random numbers on a grid, blended between grid points.</summary>
    private static float Noise(float x, float y, int seed)
    {
        int ix = (int)MathF.Floor(x), iy = (int)MathF.Floor(y);
        float fx = x - ix, fy = y - iy;
        fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
        float a = Hash(ix, iy, seed), b = Hash(ix + 1, iy, seed), c = Hash(ix, iy + 1, seed), d = Hash(ix + 1, iy + 1, seed);
        return a + (b - a) * fx + (c - a) * fy + (a - b - c + d) * fx * fy;
    }

    // ====================================================================================================
    //  The plan (Begin: all the dice are rolled here, and only here)
    // ====================================================================================================

    public override void Begin(Random rng)
    {
        _rng = rng;
        _n = 0;
        _windX = (rng.NextSingle() < 0.5f ? -1f : 1f) * _s.U * (0.008f + 0.008f * rng.NextSingle());
        _windY = -_s.U * 0.003f;

        Kind? force = null;
        string? f = Environment.GetEnvironmentVariable("PECKWORKS_FIREWORK");
        if (!string.IsNullOrWhiteSpace(f) && Enum.TryParse(f, true, out Kind fk)) force = fk;

        if (force is Kind only)
        {
            // Test mode: five shells of the same kind, four seconds apart, so each can be looked at alone.
            for (int i = 0; i < 5; i++) Add(2.0f + 4f * i, only, 1f, 1.4f);
        }
        else
        {
            Kind[] open = [Kind.Peony, Kind.Ring, Kind.Chrys, Kind.Star, Kind.Pastel];
            Shuffle(open);
            Add(1.7f + 0.2f * R(), open[0], 0.85f, 1f);
            Add(3.7f + 0.3f * R(), open[1], 0.9f, 1f);
            Add(5.6f + 0.3f * R(), open[2], 1f, 1f);

            Add(8.0f + 0.2f * R(), Kind.Snow, 1f, 1.3f);
            Kind other = R() < 0.5f ? Kind.Peony : Kind.Ring;
            Add(10.2f + 0.1f * R(), Kind.Double, 1f, 1f);
            Add(10.6f + 0.2f * R(), other, 0.95f, 1f);
            Add(12.8f + 0.2f * R(), Kind.Tree, 1f, 1.6f);

            Kind[] lump = [Kind.Cross, Kind.Chrys, Kind.Peony, Kind.Pastel];
            Shuffle(lump);
            Add(15.0f, Kind.Star, 1f, 0.8f);
            Add(15.5f + 0.1f * R(), lump[0], 1f, 0.8f);
            Add(16.0f + 0.1f * R(), lump[1], 0.95f, 0.8f);

            // THE FINALE: bigger shells than the rest of the show, and VOLLEYS (two or three bursting at the same
            // instant in a row across the sky), then a huge gold willow. Finale shells carry fewer stars each
            // (bigger and brighter, not denser) so the cost stays flat.
            Volley(18.5f, 0.30f, 1.3f, Kind.Peony, Kind.Chrys, Kind.Peony);
            Add(19.5f, Kind.Ring, 1.15f, 0.4f, fin: true);
            Add(19.8f, Kind.Pastel, 1.2f, 0.4f, fin: true);
            Volley(20.3f, 0.22f, 1.35f, Kind.Star, Kind.Chrys, null);
            Add(21.0f, Kind.Willow, 1.15f, 0.4f, fin: true);
            Add(21.3f, Kind.Cross, 1.3f, 0.4f, fin: true);
            Add(21.6f, Kind.Peony, 1.3f, 0.4f, fin: true);
            Volley(22.0f, 0.26f, 1.4f, Kind.Chrys, Kind.Willow, Kind.Chrys);
            Add(23.4f, Kind.Willow, 1.7f, 0.2f, grand: true, fx: 0.5f, fy: 0.2f, fin: true, sky: true);
        }

        float end = 0;
        for (int s = 0; s < _n; s++) end = MathF.Max(end, _tb[s] + _life[s]);
        _seconds = end + 1.3f;

        string? log = Environment.GetEnvironmentVariable("PECKWORKS_FIREWORKLOG");
        if (!string.IsNullOrWhiteSpace(log))
        {
            var sb = new System.Text.StringBuilder();
            for (int s = 0; s < _n; s++)
                sb.AppendLine($"shell {s} kind={(Kind)_kind[s]} launch={_tl[s]:F2} burst={_tb[s]:F2} at=({_bx[s] / _s.Width:F2},{_by[s] / _s.Height:F2}) R={_R[s] / _s.U:F3}U stars={_count[s]}");
            sb.AppendLine($"seconds {_seconds:F2}");
            File.AppendAllText(log, sb.ToString());
        }
    }

    private float R() => _rng.NextSingle();

    /// <summary>Two or three shells that burst at the SAME instant, in a row across the sky at about one height.</summary>
    private void Volley(float tb, float fy, float scale, Kind a, Kind b, Kind? c)
    {
        if (c is Kind kc)
        {
            Add(tb, a, scale, 0.3f, fx: 0.38f, fy: fy + 0.03f, fin: true);
            Add(tb, b, scale * 1.1f, 0.3f, fx: 0.58f, fy: fy - 0.02f, fin: true, sky: true);
            Add(tb, kc, scale, 0.3f, fx: 0.80f, fy: fy + 0.04f, fin: true);
        }
        else
        {
            Add(tb, a, scale, 0.3f, fx: 0.47f, fy: fy, fin: true, sky: true);
            Add(tb, b, scale, 0.3f, fx: 0.74f, fy: fy + 0.05f, fin: true);
        }
    }

    private void Shuffle(Kind[] a)
    {
        for (int i = a.Length - 1; i > 0; i--) { int j = _rng.Next(i + 1); (a[i], a[j]) = (a[j], a[i]); }
    }

    /// <summary>Plans one shell that BURSTS at time tb: its size, place, launch, physics and stars.</summary>
    private void Add(float tb, Kind kind, float sizeScale, float sep, bool grand = false, float fx = -1f, float fy = -1f, bool fin = false, bool sky = false)
    {
        if (_n >= MaxShells) return;
        int s = _n++;
        float U = _s.U;
        _kind[s] = (byte)kind;
        (float lo, float hi) = kind switch
        {
            Kind.Peony => (0.09f, 0.13f), Kind.Willow => (0.12f, 0.15f), Kind.Chrys => (0.09f, 0.125f),
            Kind.Ring => (0.075f, 0.10f), Kind.Double => (0.09f, 0.115f), Kind.Star => (0.09f, 0.115f),
            Kind.Snow => (0.10f, 0.125f), Kind.Tree => (0.13f, 0.15f), Kind.Cross => (0.09f, 0.115f), _ => (0.08f, 0.11f),
        };
        float Rr = U * (lo + (hi - lo) * R()) * sizeScale;
        Rr *= 1f + 0.35f * Math.Clamp((_s.Height / U - 1.2f) / 2f, 0f, 1f);   // a tall screen has more sky: bigger shells
        Rr = Math.Min(Rr, U * 0.23f);
        _R[s] = Rr;
        _big[s] = grand; _fin[s] = fin; _skyF[s] = sky;
        _keep[s] = fin && kind != Kind.Ring && kind != Kind.Double && kind != Kind.Star ? 0.6f : 1f;
        _ph[s] = R() * MathF.Tau;

        // ---- the physics of this kind of shell ----
        float drag = 2.5f, vt = 0.045f, life = 3.1f, hold = 0.35f, tau = 0.14f, tc = 0.45f, spin = 0f, fall = 0f, fallAt = 99f;
        int pieces = 2;
        switch (kind)
        {
            case Kind.Willow: drag = 1.9f; vt = grand ? 0.12f : 0.10f; life = grand ? 4.3f : 3.7f; hold = 0.45f; tau = grand ? 0.65f : 0.5f; pieces = grand ? 5 : 4; tc = 1.0f; break;
            case Kind.Chrys: drag = 2.6f; vt = 0.03f; life = 3.2f; hold = 0.4f; tau = 0.18f; pieces = 2; tc = 0.5f; break;
            case Kind.Ring: spin = (R() < 0.5f ? -1 : 1) * 0.25f; vt = 0.02f; hold = 0.3f; break;
            case Kind.Double: spin = (R() < 0.5f ? -1 : 1) * 0.2f; vt = 0.02f; life = 2.8f; hold = 0.3f; break;
            case Kind.Star: drag = 2.8f; vt = 0.012f; life = 2.8f; hold = 0.45f; tau = 0.06f; break;
            case Kind.Snow: drag = 2.6f; vt = 0.010f; life = 3.6f; hold = 0.5f; tau = 0.07f; spin = (R() < 0.5f ? -1 : 1) * 0.18f; tc = 0.55f; break;
            case Kind.Tree: drag = 3.2f; vt = 0.004f; life = 4.0f; hold = 0.6f; tau = 0.4f; pieces = 3; fall = 0.22f; fallAt = 2.4f; tc = 0.5f; break;
            case Kind.Cross: drag = 2.5f; vt = 0.03f; life = 3.0f; hold = 0.5f; tau = 0.1f; pieces = 1; tc = 0.5f; break;
        }
        _drag[s] = drag; _vt[s] = vt * U; _life[s] = life; _hold[s] = hold; _tau[s] = tau; _pieces[s] = pieces;
        _tc[s] = tc; _spin[s] = spin; _fall[s] = fall * U; _fallAt[s] = fallAt;
        _sdx[s] = _windX * (0.7f + 0.6f * R()); _sdy[s] = _windY;

        Place(s, tb, sep, kind == Kind.Tree ? 1.2f : 1f, fx, fy);
        _count[s] = Fill(s, kind);
    }

    /// <summary>
    /// Chooses where the burst is: toward the middle of the width, anywhere from just above the mountains
    /// to near the top, away from the moon's disc, and clear of any shell that is alive at about the same
    /// time. It tries a few places and keeps the best-clear one if none is clear enough.
    /// </summary>
    private void Place(int s, float tb, float sep, float topMargin, float fx = -1f, float fy = -1f)
    {
        float W = _s.Width, H = _s.Height, U = _s.U, Rr = _R[s];
        float bestScore = -1e9f, bx = W * 0.5f, by = H * 0.3f;
        if (fx >= 0)
        {
            // A planned spot (the finale's volleys): this x across, about this height, but never lower than just above the mountains.
            bx = Math.Clamp(W * fx + (R() - 0.5f) * W * 0.03f, Rr, W - Rr);
            float rg = Math.Min(Brushwork.RidgeYAt(_s.FarRange, bx), Brushwork.RidgeYAt(_s.NearRange, bx));
            by = Math.Clamp(H * fy + (R() - 0.5f) * H * 0.04f, H * 0.04f + Rr, Math.Max(H * 0.05f + Rr, rg - Rr * 0.3f - U * 0.012f));
        }
        else for (int tr = 0; tr < 18; tr++)
        {
            float tri = (R() + R()) * 0.5f;
            float x = Math.Clamp(W * (0.08f + 0.84f * tri), Rr * 1.05f, W - Rr * 1.05f);
            float ridge = Math.Min(Brushwork.RidgeYAt(_s.FarRange, x), Brushwork.RidgeYAt(_s.NearRange, x));
            float bottom = ridge - Rr * 0.3f - U * 0.012f;
            float top = Math.Min(H * 0.04f + Rr * topMargin, bottom);
            float y = bottom + (top - bottom) * R();

            float score = MathF.Sqrt((x - _s.Moon.At.X) * (x - _s.Moon.At.X) + (y - _s.Moon.At.Y) * (y - _s.Moon.At.Y)) - (_s.Moon.R + Rr * 1.1f);
            for (int j = 0; j < s; j++)
            {
                if (MathF.Abs(_tb[j] - tb) > 2.6f) continue;
                float d = MathF.Sqrt((x - _bx[j]) * (x - _bx[j]) + (y - _by[j]) * (y - _by[j])) - (Rr + _R[j]) * sep;
                score = MathF.Min(score, d);
            }
            if (score > bestScore) { bestScore = score; bx = x; by = y; }
            if (score >= 0) break;
        }
        _bx[s] = bx; _by[s] = by;

        // Launch from the village in the valley (or the middle if there is none), leaning toward the burst.
        float vx = 0;
        foreach (PointF p in _s.VillageWindows) vx += p.X;
        vx = _s.VillageWindows.Count > 0 ? vx / _s.VillageWindows.Count : W * 0.45f;
        _x0[s] = Math.Clamp(vx * 0.5f + bx * 0.5f + (R() - 0.5f) * W * 0.08f, W * 0.02f, W * 0.98f);
        _y0[s] = _s.Horizon + U * 0.01f;
        float dist = MathF.Sqrt((bx - _x0[s]) * (bx - _x0[s]) + (by - _y0[s]) * (by - _y0[s]));
        _lift[s] = 0.75f + 0.95f * dist / U;
        if (tb - _lift[s] < 0.1f) tb = _lift[s] + 0.1f;
        _tb[s] = tb;
        _tl[s] = tb - _lift[s];
    }

    private void Put(ref int c, int b, float ox, float oy, int col, int flag)
    {
        if (c >= MaxStars) return;
        int k = b + c++;
        _ox[k] = ox; _oy[k] = oy; _col[k] = (byte)col; _flag[k] = (byte)flag;
    }

    /// <summary>Rolls every star's final spot and look. Returns how many stars the shell has.</summary>
    private int Fill(int s, Kind kind)
    {
        int b = s * MaxStars, c = 0;
        float Rr = _R[s];
        float Rand() => _rng.NextSingle();
        void Sphere(float reachLo, out float x, out float y)
        {
            float z = 2 * Rand() - 1, rr = MathF.Sqrt(1 - z * z), a = Rand() * MathF.Tau;
            float reach = Rr * (reachLo + (1 - reachLo) * Rand());
            x = rr * MathF.Cos(a) * reach; y = rr * MathF.Sin(a) * reach;
        }
        switch (kind)
        {
            case Kind.Peony:
            case Kind.Pastel:
                {
                    int ca, cb;
                    if (kind == Kind.Peony) { float q = Rand(); (ca, cb) = q < 0.6f ? (Red, Green) : q < 0.75f ? (Red, White) : (Gold, Ice); }
                    else { float q = Rand(); (ca, cb) = q < 0.34f ? (Pink, Ice) : q < 0.67f ? (Violet, Ice) : (Pink, Gold); }
                    bool halves = Rand() < 0.5f;
                    int n = kind == Kind.Peony ? 100 : 90;
                    for (int j = 0; j < n; j++)
                    {
                        Sphere(0.82f, out float x, out float y);
                        int col = halves ? (x > 0 ? ca : cb) : (j % 2 == 0 ? ca : cb);
                        Put(ref c, b, x, y, col, Rand() < 0.15f ? 1 : 0);
                    }
                    break;
                }
            case Kind.Willow:
                {
                    int n = _big[s] ? 124 : 104;
                    for (int j = 0; j < n; j++)
                    {
                        Sphere(0.5f, out float x, out float y);
                        float q = Rand();
                        Put(ref c, b, x, y, q < 0.78f ? Gold : q < 0.9f ? White : Orange, _big[s] && Rand() < 0.5f ? 1 : 0);
                    }
                    break;
                }
            case Kind.Chrys:
                {
                    bool gold = Rand() < 0.25f;
                    for (int j = 0; j < 110; j++)
                    {
                        Sphere(0.88f, out float x, out float y);
                        Put(ref c, b, x, y, j % 3 == 0 ? (gold ? Gold : Silver) : White, Rand() < 0.38f ? 1 : 0);
                    }
                    break;
                }
            case Kind.Ring:
                {
                    int col = Rand() < 0.45f ? Red : Rand() < 0.5f ? Gold : Green;
                    float asp = 0.3f + 0.45f * Rand(), roll = Rand() * MathF.PI, cr = MathF.Cos(roll), sr = MathF.Sin(roll);
                    for (int j = 0; j < 46; j++)
                    {
                        float a = (j + 0.3f * (Rand() - 0.5f)) / 46f * MathF.Tau;
                        float x = Rr * MathF.Cos(a), y = Rr * MathF.Sin(a) * asp;
                        Put(ref c, b, x * cr - y * sr, x * sr + y * cr, col, 0);
                    }
                    for (int j = 0; j < 8; j++)     // a little white pistil in the middle
                    {
                        float a = Rand() * MathF.Tau, d = Rr * 0.22f * MathF.Sqrt(Rand());
                        Put(ref c, b, MathF.Cos(a) * d, MathF.Sin(a) * d * 0.7f, White, 0);
                    }
                    break;
                }
            case Kind.Double:
                {
                    for (int ring = 0; ring < 2; ring++)
                    {
                        float asp = 0.3f + 0.4f * Rand(), roll = (ring == 0 ? 0.2f : 1.1f) + Rand() * 0.5f, cr = MathF.Cos(roll), sr = MathF.Sin(roll);
                        float rad = ring == 0 ? Rr : Rr * 0.6f;
                        int n = ring == 0 ? 48 : 34;
                        for (int j = 0; j < n; j++)
                        {
                            float a = (j + 0.3f * (Rand() - 0.5f)) / n * MathF.Tau;
                            float x = rad * MathF.Cos(a), y = rad * MathF.Sin(a) * asp;
                            Put(ref c, b, x * cr - y * sr, x * sr + y * cr, ring == 0 ? Green : Red, 0);
                        }
                    }
                    break;
                }
            case Kind.Star:
                {
                    float rot = -MathF.PI / 2 + (Rand() - 0.5f) * 0.4f;
                    for (int i = 0; i < 10; i++)
                    {
                        float a0 = rot + i * MathF.PI / 5, a1 = rot + (i + 1) * MathF.PI / 5;
                        float r0 = i % 2 == 0 ? Rr : Rr * 0.382f, r1 = i % 2 == 0 ? Rr * 0.382f : Rr;
                        float x0 = r0 * MathF.Cos(a0), y0 = r0 * MathF.Sin(a0), x1 = r1 * MathF.Cos(a1), y1 = r1 * MathF.Sin(a1);
                        for (int m = 0; m < 8; m++)
                        {
                            float tt = m / 8f;
                            bool tip = m == 0 && i % 2 == 0;
                            Put(ref c, b, x0 + (x1 - x0) * tt, y0 + (y1 - y0) * tt, tip ? White : (m % 2 == 0 ? Gold : White), tip ? 2 : 0);
                        }
                    }
                    break;
                }
            case Kind.Snow:
                {
                    float rot = Rand() * MathF.Tau;
                    for (int arm = 0; arm < 6; arm++)
                    {
                        float a = rot + arm * MathF.PI / 3, ca = MathF.Cos(a), sa = MathF.Sin(a);
                        for (int m = 0; m < 9; m++)
                        {
                            float tt = 0.12f + m * 0.11f;
                            bool tipM = m == 8;
                            Put(ref c, b, ca * Rr * tt, sa * Rr * tt, tipM ? White : Ice, tipM ? 2 : 0);
                        }
                        for (int bar = 0; bar < 2; bar++)
                        {
                            float at = bar == 0 ? 0.45f : 0.72f, len = bar == 0 ? 0.24f : 0.17f;
                            for (int side = -1; side <= 1; side += 2)
                            {
                                float ba = a + side * MathF.PI / 3, bcx = MathF.Cos(ba), bsx = MathF.Sin(ba);
                                for (int m = 1; m <= 2; m++)
                                    Put(ref c, b, ca * Rr * at + bcx * Rr * len * m / 2f, sa * Rr * at + bsx * Rr * len * m / 2f, White, 0);
                            }
                        }
                    }
                    break;
                }
            case Kind.Tree:
                {
                    void Tier(float apexY, float botY, float hw)
                    {
                        for (int i = 1; i <= 8; i++)
                        {
                            float tt = i / 8f, y = apexY + (botY - apexY) * tt, w = hw * tt;
                            Put(ref c, b, -w * Rr, y * Rr, Green, 0);
                            Put(ref c, b, w * Rr, y * Rr, Green, 0);
                        }
                        for (int i = -3; i <= 3; i++) Put(ref c, b, hw * Rr * i / 4f, botY * Rr, Green, 0);
                        float my = apexY + (botY - apexY) * 0.55f, mw = hw * 0.55f;
                        for (int i = -1; i <= 1; i++) Put(ref c, b, mw * Rr * 0.55f * i, my * Rr, Green, 0);
                    }
                    Tier(-1.0f, -0.4f, 0.34f);
                    Tier(-0.72f, 0.08f, 0.58f);
                    Tier(-0.38f, 0.62f, 0.85f);
                    int shape = c;
                    Put(ref c, b, -0.09f * Rr, 0.72f * Rr, Orange, 0);
                    Put(ref c, b, 0.09f * Rr, 0.72f * Rr, Orange, 0);
                    Put(ref c, b, -0.09f * Rr, 0.84f * Rr, Orange, 0);
                    Put(ref c, b, 0.09f * Rr, 0.84f * Rr, Orange, 0);
                    Put(ref c, b, 0, -1.1f * Rr, Gold, 2 | 16);                          // the star on top
                    int[] bauble = [Red, Gold, Ice, Red, Gold, Ice];
                    for (int j = 0; j < 12; j++)                                      // baubles: some of the green stars turn colour and glow bigger
                    {
                        int k = b + (int)(Rand() * (shape - 5));
                        _col[k] = (byte)bauble[j % bauble.Length]; _flag[k] = 2;
                    }
                    break;
                }
            default:    // Cross: a peony of gold stars, each of which splits into four after 0.6 s
                for (int j = 0; j < 46; j++)
                {
                    Sphere(0.78f, out float x, out float y);
                    Put(ref c, b, x, y, Rand() < 0.8f ? Gold : Orange, 32);
                }
                break;
        }
        for (int j = 0; j < c; j++)
        {
            int k = b + j;
            _sph[k] = Rand() * MathF.Tau;
            _rate[k] = 9 + 14 * Rand();
            _lf[k] = 0.82f + 0.18f * Rand();
            _gm[k] = kind == Kind.Tree ? 0.6f + 1.1f * Rand() : 0.85f + 0.35f * Rand();
            _rank[k] = Rand() * ((_flag[k] & 2) != 0 || kind == Kind.Tree ? 0.3f : 1f);
        }
        return c;
    }

    // ====================================================================================================
    //  Drawing (everything from t alone)
    // ====================================================================================================

    private static float Sq(float x) => x * x;

    /// <summary>Stamps a small dot at its true position, using the sprite painted nearest to the fractional part.</summary>
    private void Dot(FrameBuffer fb, int size, int col, float x, float y, float alpha)
    {
        if (alpha <= 0.01f) return;
        int qx = (int)MathF.Round(x * 4), qy = (int)MathF.Round(y * 4);
        int half = (size == 0 ? _r0 : _r1) + 1;
        _dot[size][col * 16 + ((qy & 3) << 2) + (qx & 3)].Draw(fb, (qx >> 2) - half, (qy >> 2) - half, alpha, _s.OpenSky);
    }

    /// <summary>Where a rising shell is, u of the way up (0 to 1): quick off the mark, slowing as it climbs, with a faint wobble.</summary>
    private PointF Path(int s, float u)
    {
        float e = 1f - MathF.Pow(1f - u, 2.2f);
        float wob = _s.U * 0.0035f * MathF.Sin(u * 9f + _ph[s]) * (1f - u);
        return new PointF(_x0[s] + (_bx[s] - _x0[s]) * e + wob, _y0[s] + (_by[s] - _y0[s]) * e);
    }

    /// <summary>The k-th largest of the positive values (0 if fewer than k are positive): the line below which a glow is skipped.</summary>
    private float Threshold(float[] v, int count, int k)
    {
        int m = 0;
        for (int i = 0; i < count; i++)
        {
            float x = v[i];
            if (x <= 0.0001f) continue;
            if (m == k && x <= _top[k - 1]) continue;
            int p = m < k ? m++ : k - 1;
            while (p > 0 && _top[p - 1] < x) { _top[p] = _top[p - 1]; p--; }
            _top[p] = x;
        }
        return m < k ? 0f : _top[k - 1];
    }

    private static float Cap(float s, float thr) => thr <= 0 || s >= thr ? 1f : Smooth((s - 0.7f * thr) / (0.3f * thr));

    public override void Draw(FrameBuffer fb, float t)
    {
        float U = _s.U;
        float gEnd = Smooth((_seconds - t) / 1.2f);

        // ---- Pass 0: how strong is each shell's sky glow and smoke now, and how many stars are alive ----
        float live = 0;
        for (int s = 0; s < _n; s++)
        {
            float a = t - _tb[s];
            _amS[s] = 0; _smS[s] = 0;
            if (a < 0) continue;
            _wsS[s] = 0;
            if (a < _life[s]) live += _count[s] * _keep[s] * (1f - a / _life[s]);
            float amb = Smooth(a / 0.05f) * MathF.Exp(-a / _tc[s]);
            // The tree also glows green behind its whole shape while it holds.
            if ((Kind)_kind[s] == Kind.Tree) amb = MathF.Max(amb, 0.8f * Smooth(a / 0.4f) * (1f - Smooth((a - 2.2f) / 1.4f)));
            if (a < 0.6f) _wsS[s] = Smooth(a / 0.06f) * (1f - Smooth(a / 0.6f)) * (0.6f + 0.4f * _R[s] / (0.12f * U));
            _amS[s] = amb > 0.03f ? amb * (0.55f + 0.45f * _R[s] / (0.12f * U)) : 0f;
            if (_R[s] > 0.06f * U)
            {
                float sm = Smooth((a - 0.2f) / 1.0f) * (1f - Smooth((a - 1.8f) / 4.5f)) * gEnd;
                _smS[s] = sm > 0.01f ? sm : 0f;
            }
        }
        float cut = Math.Min(1f, LiveBudget / Math.Max(1f, live));
        float ambThr = Threshold(_amS, _n, 4), smThr = Threshold(_smS, _n, 5), wsThr = Threshold(_wsS, _n, 3);

        // ---- Pass 1: the ghosts of smoke, then the coloured light on sky, mountains and valley ----
        for (int s = 0; s < _n; s++)
        {
            float sm = _smS[s];
            if (sm <= 0) continue;
            float a = t - _tb[s];
            Sprite sp = _smoke[_R[s] >= 0.1f * U ? 1 : 0];
            sp.DrawCentered(fb, _bx[s] + _sdx[s] * a, _by[s] + _sdy[s] * a + _R[s] * 0.15f, 0.26f * sm * Cap(sm, smThr), _s.OpenSky);
        }
        for (int s = 0; s < _n; s++)
        {
            float am = _amS[s];
            if (am <= 0) continue;
            int cc = ShellColour(s);
            _amb[_R[s] >= 0.1f * U ? 1 : 0][cc].DrawCentered(fb, _bx[s], _by[s], 0.34f * am * Cap(am, ambThr), _s.OpenValley);
        }
        // The snowfield catches the colour: a faint wide wash below each burst for its first half second, only
        // where the stencil says snow (never the sky). Peak alpha stays under 30 of 255.
        for (int s = 0; s < _n; s++)
        {
            float ws = _wsS[s];
            if (ws <= 0.002f || NoWash) continue;
            float cy = _s.Ground.YAt(Math.Clamp(_bx[s], 0, _s.Width - 1)) + U * 0.09f;
            _wash[ShellColour(s)].DrawCentered(fb, _bx[s], cy, 0.105f * ws * Cap(ws, wsThr), _snow);
        }
        // The whole-sky flash of the biggest shells: in over two frames, out over about twelve.
        for (int s = 0; s < _n; s++)
        {
            if (!_skyF[s]) continue;
            float a = t - _tb[s];
            if (a < 0 || a > 0.3f) continue;
            float v = Smooth(a / 0.033f) * (1f - Smooth((a - 0.033f) / 0.2f));
            if (v > 0.01f) _skyFlash.DrawCentered(fb, _bx[s], _by[s], (_big[s] ? 0.16f : 0.10f) * v, _s.OpenValley);
        }

        // ---- Pass 2: each shell's climb, its stars and its flash ----
        for (int s = 0; s < _n; s++)
        {
            if (t < _tl[s]) continue;
            float aB = t - _tb[s];
            if (aB > _life[s]) continue;
            if (aB < 0.7f) Climb(fb, s, t, aB);
            if (aB < 0) continue;
            Stars(fb, s, aB, cut);
            if (aB < 0.2f)
            {
                float fl = 1f - aB / 0.2f;
                _flash.DrawCentered(fb, _bx[s], _by[s], 0.95f * fl * fl, _s.OpenSky);
                if (_fin[s]) _flash.DrawCentered(fb, _bx[s], _by[s], 0.9f * fl * fl, _s.OpenSky);     // the finale's brighter core
            }
        }
    }

    /// <summary>The colour that lights the sky for a shell: its main star colour.</summary>
    private int ShellColour(int s) => _col[s * MaxStars + (_count[s] > 1 ? 1 : 0)];

    private static readonly Color Warm = Color.FromArgb(255, 214, 150);

    private void Climb(FrameBuffer fb, int s, float t, float aB)
    {
        float U = _s.U;
        float u = (t - _tl[s]) / _lift[s];
        float fade = aB <= 0 ? 1f : 1f - Smooth(aB / 0.7f);
        float lead = Smooth(u / 0.1f);
        if (fade <= 0.01f) return;
        float uh = Math.Min(1f, u);
        float th = Math.Max(1.2f, U * 0.0022f);

        // The trail: seven short pieces, brightest at the head, fading backward. After the burst the head stays
        // at the burst point (uh = 1) so the trail hangs where it was and just fades.
        PointF prev = Path(s, uh);
        for (int j = 1; j <= 7; j++)
        {
            PointF cur = Path(s, Math.Max(0f, uh - j * 0.05f));
            float al = 0.6f * MathF.Pow(1f - (j - 1) / 7f, 1.5f) * fade * lead;
            fb.Line(prev, cur, Warm, al, th, _s.OpenSky);
            prev = cur;
        }
        // Glitter: a few sparks left behind the head, each on its own flicker.
        for (int g = 0; g < 4; g++)
        {
            float f = MathF.Sin(t * 31f + g * 2.3f + s * 1.7f);
            if (f < 0.1f) continue;
            PointF p = Path(s, Math.Max(0f, uh - (g + 1) * 0.07f));
            Dot(fb, 0, Gold, p.X + U * 0.004f * MathF.Sin(g * 5f + s), p.Y + U * 0.002f * g, 0.7f * f * fade * lead);
        }
        if (aB <= 0)
        {
            PointF h = Path(s, uh);
            float pulse = 0.85f + 0.15f * MathF.Sin(t * 45f);
            Dot(fb, 1, Gold, h.X, h.Y, 0.9f * lead * pulse);
            Dot(fb, 0, White, h.X, h.Y, 1f * lead);
        }
    }

    private void Stars(FrameBuffer fb, int s, float a, float cut)
    {
        float U = _s.U, bx = _bx[s], by = _by[s], life = _life[s], hold = _hold[s];
        int P = _pieces[s], b = s * MaxStars;
        Kind kind = (Kind)_kind[s];
        float tauStep = P > 0 ? _tau[s] / P : 0f;

        // Everything that is the same for every star of this shell, at "now" (m = 0) and at each tail point
        // (m = 1..P, each a little earlier): the drag growth, the turn of the whole shape, the sinking.
        for (int m = 0; m <= P; m++)
        {
            float am = MathF.Max(0f, a - m * tauStep);
            _E[m] = 1f - MathF.Exp(-_drag[s] * am);
            float ang = _spin[s] * am;
            _C[m] = MathF.Cos(ang); _S[m] = MathF.Sin(ang);
            _Dd[m] = _vt[s] * (am - (1f - MathF.Exp(-1.3f * am)) / 1.3f) + _fall[s] * Sq(MathF.Max(0f, am - _fallAt[s]));
        }
        float th = Math.Max(1.2f, U * (kind == Kind.Willow ? 0.0026f : 0.0018f));
        float flutAmp = U * 0.012f * Smooth(a / 1.0f);

        for (int j = 0; j < _count[s]; j++)
        {
            int k = b + j;
            if (_rank[k] > _keep[s]) continue;                                   // finale shells carry fewer stars (a fixed choice, so nothing blinks)
            float x = a / (_lf[k] * life);
            if (x >= 1f) continue;
            float dens = Math.Clamp((cut * 1.15f - _rank[k]) * 6f, 0f, 1f);
            if (dens <= 0f) continue;

            float env = (1f - Smooth((x - hold) / (1f - hold))) * Smooth(a / 0.05f);
            float al = env * dens * (0.84f + 0.16f * MathF.Sin(a * _rate[k] + _sph[k]));
            int flag = _flag[k], col = _col[k];
            if ((flag & 4) != 0) al *= 0.4f + 0.6f * MathF.Abs(MathF.Sin(a * 3.7f + _sph[k]));     // a leaf turning over
            if (al < 0.015f) continue;

            float ox = _ox[k], oy = _oy[k], gm = _gm[k];
            float fl = (flag & 4) != 0 ? flutAmp * MathF.Sin(a * 2.4f + _sph[k]) : 0f;
            float px = bx + (ox * _C[0] - oy * _S[0]) * _E[0] + fl;
            float py = by + (ox * _S[0] + oy * _C[0]) * _E[0] + gm * _Dd[0];

            if ((flag & 32) != 0)
            {
                // CROSSETTE: at 0.6 s the star splits into four that fly off in a cross, carried along by the
                // parent's own drift. The parent fades out just before, so the split is a pop of light, not a jump.
                const float ts = 0.6f;
                if (a >= ts)
                {
                    float ca = a - ts, spread = _R[s] * 0.17f * (1f - MathF.Exp(-5f * ca)), ramp = Smooth(ca / 0.06f);
                    for (int q = 0; q < 4; q++)
                    {
                        float ang = _sph[k] + q * 1.5708f, cs = MathF.Cos(ang), sn = MathF.Sin(ang);
                        if (ca < 0.45f)
                            fb.Line(new PointF(px + cs * spread * 0.3f, py + sn * spread * 0.3f), new PointF(px + cs * spread, py + sn * spread), Pal[col], al * 0.55f * (1f - ca / 0.45f), th, _s.OpenSky);
                        Dot(fb, 1, col, px + cs * spread, py + sn * spread, al * ramp);
                    }
                    if (ca < 0.14f)
                    {
                        float f3 = 1f - ca / 0.14f;
                        _flare[Gold].DrawCentered(fb, px, py, 0.8f * f3 * f3 * dens, _s.OpenSky);
                    }
                    continue;
                }
                al *= 1f - Smooth((a - ts + 0.04f) / 0.04f);
                if (al < 0.015f) continue;
            }

            // The tail: from now back through the earlier points, fading toward the end.
            float qx = px, qy = py;
            for (int m = 1; m <= P; m++)
            {
                float rx = bx + (ox * _C[m] - oy * _S[m]) * _E[m] + fl;
                float ry = by + (ox * _S[m] + oy * _C[m]) * _E[m] + gm * _Dd[m];
                float ta = al * (0.62f - 0.5f * (m - 1) / Math.Max(1, P));
                if (ta > 0.02f && (qx - rx) * (qx - rx) + (qy - ry) * (qy - ry) > 0.5f)
                    fb.Line(new PointF(qx, qy), new PointF(rx, ry), Pal[kind == Kind.Tree && a > 2.7f ? Gold : col], ta, th, _s.OpenSky);
                qx = rx; qy = ry;
            }

            if ((flag & 2) != 0)
            {
                // A big star: a bauble, a tip, the star on the tree. A larger soft halo that breathes a little.
                // On the tree the baubles twinkle hard (each on its own beat) and the top star gets a four-point sparkle.
                bool tree = kind == Kind.Tree;
                float pulse = tree ? 0.55f + 0.45f * MathF.Sin(a * 9f + _sph[k]) : 0.78f + 0.22f * MathF.Sin(a * 6f + _sph[k]);
                _flare[col].DrawCentered(fb, px, py, (tree ? 1f : 0.85f) * al * pulse, _s.OpenSky);
                Dot(fb, 1, col, px, py, al);
                Dot(fb, 0, White, px, py, al * (tree ? pulse : 1f));
                if ((flag & 16) != 0)
                {
                    float len = U * 0.04f * (0.8f + 0.2f * MathF.Sin(a * 5f)) * Smooth(a / 0.4f);
                    _flare[Gold].DrawCentered(fb, px, py, 0.9f * al, _s.OpenSky);
                    // Four spikes, each in two pieces (bright inside, faint at the tip) so they taper like a real sparkle.
                    Color sc = Color.FromArgb(255, 245, 200);
                    for (int sp = 0; sp < 4; sp++)
                    {
                        float dx = sp == 0 ? 1 : sp == 1 ? -1 : 0, dy = sp == 2 ? 1 : sp == 3 ? -1 : 0;
                        fb.Line(new PointF(px, py), new PointF(px + dx * len * 0.5f, py + dy * len * 0.5f), sc, 0.9f * al, 2.2f, _s.OpenSky);
                        fb.Line(new PointF(px + dx * len * 0.5f, py + dy * len * 0.5f), new PointF(px + dx * len, py + dy * len), sc, 0.4f * al, 1.2f, _s.OpenSky);
                    }
                }
            }
            else if (kind == Kind.Tree)
            {
                // The tree's green stars: bigger and brighter than any other, and in the last second they
                // turn to gold as they rain down.
                int tx = (int)MathF.Round(px * 4), ty = (int)MathF.Round(py * 4), half = _rT + 1;
                _dotT[((ty & 3) << 2) + (tx & 3)].Draw(fb, (tx >> 2) - half, (ty >> 2) - half, MathF.Min(1f, al * 1.3f), _s.OpenSky);
                Dot(fb, 1, White, px, py, al * 0.55f);
                float rain = Smooth((a - 2.4f) / 0.5f);
                if (rain > 0f) Dot(fb, 1, Gold, px, py, al * rain);
            }
            else
            {
                Dot(fb, 1, col, px, py, al);
                if (a < 0.3f && (j % 3) == 0)
                {
                    float f2 = 1f - a / 0.3f;
                    _flare[col].DrawCentered(fb, px, py, 0.6f * f2 * f2 * dens, _s.OpenSky);     // the first burst of light
                }
            }

            // Crackle: in the second half of its life, a crackling star flashes tiny white sparks, on purpose.
            if ((flag & 1) != 0 && x > 0.45f)
            {
                float p = MathF.Sin(a * 43f + _sph[k]) * MathF.Sin(a * 29f + _sph[k] * 1.7f);
                if (p > 0.35f) Dot(fb, 0, White, px, py, MathF.Min(1f, p * 1.6f) * dens * MathF.Min(1f, env * 3f));
            }
        }
    }
}
