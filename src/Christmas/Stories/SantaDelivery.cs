using Peckworks.Screensavers.Core;
using Peckworks.Screensavers.Core.Minds;

namespace Christmas.Stories;

/// <summary>
/// SANTA DELIVERS (about 40 seconds). The team of NINE (four pairs and Rudolph) comes in low from the right on a
/// long descending curve. The roof is short, so only the SLEIGH and the REAR PAIR land on it (the pair with a puff of snow
/// each, the sleigh sliding in behind them until its runners rest on the ridge, left of the chimney); Rudolph and the other
/// three pairs overshoot the eave and hold station in the air beyond it, in a gentle hover: a slow collected canter on the
/// spot, bodies bobbing out of step, the gang line taut back to the sleigh and Rudolph's nose glowing out in front. Santa stands up, shoulders his sack, turns, climbs over the
/// sleigh's back, hops onto the chimney and goes down it feet first, sticking at the
/// belly. The cabin's windows flicker and brighten one after the other, a little Santa-with-
/// a-sack shadow crosses the right window and the lights on the family's tree blink. The
/// back pair of reindeer look round over their shoulders at the chimney. He comes back up
/// hat first, sooty, pats himself clean, turns, hops back into the sleigh, cracks the
/// reins, and the team leaps off the roof in order and climbs away to the upper left in
/// a burst of stardust (Rudolph leans in, the gang line tightens pair by pair, the sleigh lifts off last).
///
/// PERSPECTIVE. The cabin is farther away than the sky lane Santa flies in, so the whole
/// rig SHRINKS as it comes down to it (to 0.62 of its flying size) and grows again on the
/// way out. Every size is its own set of pictures (SantaRig's size ladder: seven rungs, no
/// two more than 8 percent apart); nothing is ever scaled per frame. The size is a function
/// of how far Rudolph has come (K), and every length in this file is "so many s" where s is
/// the size of the rung in use (S(t)), so the team's spacing shrinks with it.
///
/// HOW IT IS BUILT. Every picture is a pure function of the story's clock t: Begin works
/// out the places and the few times that cannot be written down by hand (when each reindeer
/// touches down, when its legs should switch from the gallop to the landing), and from then
/// on Update(t) and Draw(t) only ask "where is everybody at t". The same seed plays the same
/// story, and any second of it can be rendered on its own.
///
/// The team follows its leader. Rudolph's position is one number (RX), worked out from t
/// (cruise, then a braking run along the ridge, then later an acceleration). Each other part
/// of the team sits a fixed distance behind him, and its height comes from ITS OWN place on
/// the curve (the curve is a height for each x). So the back of the team is higher on the
/// descent and touches down later: they land one after another with no extra rules.
///
/// WHERE THE ROOF IS. The roof's top edge, the chimney's rim and the snow under the runners
/// are MEASURED off the painted pixels in Begin (the first snow-bright row from above), so
/// hooves and runners sit on the painted snow, not a pixel above it or inside it.
///
/// WHAT IT IS SOLID TO. The reindeer stand on the ridge; the front pair may stand in the air
/// a little past the eave; the sleigh's runners rest on the ridge with its whole length over
/// the roof; Santa stands INSIDE the sleigh (drawn before it, so its side hides his legs)
/// and is drawn through a stencil that hides everything below the chimney's rim while he
/// goes in. Everything is drawn in front of the cabin and the pines and behind nothing
/// except the corner branches (OpenOfBranches).
///
/// WHAT IT PINS: Movement at the roof (reach 1.0 U, weight 0.6) about once a second with the
/// rig as "who", so the animals watch; one Loud (reach 0.5 U, weight 0.5) at the landing.
/// </summary>
internal sealed class SantaDelivery : Story
{
    // ---- The timeline (seconds on the story's clock) ----
    private const float TApp = 7.5f;                    // Rudolph reaches the roof's right end
    private const float TRise = 11.5f;                  // Santa springs to his feet in the sleigh
    private const float TTurn1 = 12.05f;                // he turns to face the chimney (the sleigh faces the other way)
    private const float TWalk = 12.5f;                  // he climbs to the sleigh's back
    private const float THop = 13.4f;                   // and hops to the chimney's rim
    private const float TLift = 14.9f;                  // he holds the sack overhead and begins to sink
    private const float TIn = 17.1f;                    // gone
    private const float TOut = 24.7f;                   // hat first
    private const float TDust = 26.6f;                  // pats the soot off
    private const float TTurn = 28.1f;                  // turns toward the sleigh
    private const float THopBack = 28.5f;
    private const float TWalkBack = 29.25f;
    private const float TSitStart = 30.3f;
    private const float TSit = 30.75f;
    private const float TWave = 31.3f;
    private const float TCrack = 32.5f;
    private const float TDep = 32.9f;                   // the team starts to run

    // Where each part of the team sits behind Rudolph, in s (the same spacing in the air and on the roof: the team never huddles;
    // the roof is simply not where most of it is). Member 0 is Rudolph, pair p is members 2p-1 (near) and 2p (far); member 9 is the sleigh.
    private const int M = SantaRig.Members, SleighIdx = M;                           // 9 reindeer; the sleigh is "member 9"
    private static readonly float[] Off = MakeOff(), DyStory = MakeDy();
    private static float[] MakeOff() { var a = new float[M + 1]; for (int i = 0; i < M; i++) a[i] = -SantaRig.Dx[i]; a[M] = -SantaRig.SleighBack; return a; }
    /// <summary>Heights on the roof: the NEAR reindeer of the rear pair stands with its hooves on the ridge, the far one a hair higher (farther along the roof).</summary>
    private static float[] MakeDy() { var a = new float[M]; for (int i = 0; i < M; i++) a[i] = SantaRig.Dy[i] - 0.10f; return a; }
    private static readonly float[] PhaseOff = SantaRig.PhaseOff;
    private const float Rate = 1.5f;                    // gallop beats a second (the same as the flight)
    private const float HoverRate = 0.33f;              // the hover's beat as a fraction of the gallop's (0.5 a second: a page every six frames)
    private const float TbMin = 1.8f;
    private const float RestSleighFromLeft = 2.25f;     // the sleigh's rest, in roof s from the ridge's left end (the rear pair a little ahead of it, the rest past the eave)
    private const float LiftMax = 0.9f;                 // how high the hovering team rides above the ridge, in roof s, at the far end of the team
    private static bool Stands(int i) => i >= M - 2 && i < M;            // the rear pair (members 7 and 8) stand on the ridge
    private static float Delay(int i) => i == SleighIdx ? 0.45f : 0.09f * ((i + 1) / 2);   // at the getaway the pull travels back down the line: Rudolph first, the sleigh last

    private struct Ev { public float T0, X, Y, Vx, Vy, Life; public byte Kind; }   // a puff: where and when it starts, how it moves, how long it lives; kinds below
    private const byte KSnow = 0, KSnowBig = 1, KSpark = 2, KSoot = 3, KVapour = 4;

    private readonly ChristmasWorld _w;
    private readonly ChristmasScenery _sc;
    private readonly SantaRig _rig;
    private readonly Sprite _snow, _snowBig, _spark, _soot, _vapour, _winGlow;
    private readonly Sprite[] _treeGlow;
    private bool[]? _clip;
    private Ev[] _ev = [];

    // Geometry, worked out in Begin from the scenery's facts and its pixels.
    private float _sky, _sR, _u, _cx, _ridgeY, _ridgeL, _ridgeR, _rimX, _rimY, _rx0, _rxF, _xB, _xTd, _xEdge, _v, _tStop, _vmax, _acc, _tAcc, _hMax, _xShrink;
    private float _hMaxS, _xTdS, _xStartS, _feetR, _runnerR;
    private float _tStopAll, _vLeap;
    private readonly float[] _tH = new float[M], _tSw = new float[M], _tTd = new float[M], _tEdge = new float[M], _xRest = new float[M + 1];
    // The hovering members' beat: a phase that adds up their own speed (so the gallop slows into the hover without a hitch), and the
    // moments they step from the gallop to the middle gait to the hover gait (at the gather of their own beat, where the pictures differ least).
    private const int PhiHz = 120;
    private float[][] _phi = [];
    private readonly List<(float T, int Level)>[] _lv = new List<(float, int)>[M];
    private float _sx, _sy, _sTilt;
    private int _lastPin;
    private bool _burst1, _burst2, _loud;

    public SantaDelivery(ChristmasWorld w)
    {
        _w = w; _sc = w.Scenery; _rig = w.Santa;
        _u = _sc.U;
        int r = Math.Max(3, (int)(_u * 0.0075f));
        _snow = Sprite.Paint(r * 2, r * 2, g => Soft(g, r, Color.FromArgb(235, 244, 255), 200));
        int rb = Math.Max(4, (int)(_u * 0.013f));
        _snowBig = Sprite.Paint(rb * 2, rb * 2, g => Soft(g, rb, Color.FromArgb(226, 238, 255), 170));
        _soot = Sprite.Paint(rb * 2, rb * 2, g => Soft(g, rb, Color.FromArgb(44, 40, 46), 200));
        int rv = Math.Max(3, (int)(_u * 0.0085f));
        _vapour = Sprite.Paint(rv * 2, rv * 2, g => Soft(g, rv, Color.FromArgb(240, 246, 255), 120));
        _spark = SantaRig.PaintSparkle(Math.Max(6, (int)(_u * 0.016f)));
        _winGlow = Sprite.Glow(Math.Max(3, (int)(_u * 0.030f)), Color.FromArgb(255, 196, 100));
        Color[] colours = [Color.FromArgb(255, 90, 90), Color.FromArgb(110, 170, 255), Color.FromArgb(255, 240, 190)];
        _treeGlow = new Sprite[3];
        for (int i = 0; i < 3; i++) _treeGlow[i] = Sprite.Glow(Math.Max(2, (int)(_u * 0.0065f)), colours[i]);
    }

    /// <summary>A soft round puff: solid in the middle, fading to nothing at the edge.</summary>
    private static void Soft(Graphics g, int radius, Color c, int centreAlpha)
    {
        using var path = new System.Drawing.Drawing2D.GraphicsPath();
        path.AddEllipse(0, 0, radius * 2, radius * 2);
        using var br = new System.Drawing.Drawing2D.PathGradientBrush(path)
        {
            CenterColor = Color.FromArgb(centreAlpha, c),
            SurroundColors = [Color.FromArgb(0, c)],
            Blend = Sprite.SoftFalloff,
        };
        g.FillPath(br, path);
    }

    public override string Name => nameof(SantaDelivery);
    public override string Claims => "santa,cabin,skyshow";
    // The middle sizes are painted the first time a story asks (a helper thread, a second or two) and let go when it ends.
    public override bool CanBegin => _rig.Ready && !_rig.Directed && !_sc.SantaOnScreen && _rig.EnsureLadder();

    // ==================================================================
    //  THE SCRIPT'S NUMBERS (Begin)
    // ==================================================================

    /// <summary>The first row, going down from "from" to "to", whose pixel is snow-bright (all three channels high), or -1.</summary>
    private int FirstSnowRow(int x, int from, int to)
    {
        uint[] px = _sc.Pixels;
        int w = _sc.Width;
        if (x < 0 || x >= w) return -1;
        for (int y = Math.Max(0, from); y < Math.Min(_sc.Height, to); y++)
        {
            uint p = px[y * w + x];
            if (((p >> 16) & 0xFF) >= 200 && ((p >> 8) & 0xFF) >= 214 && (p & 0xFF) >= 232) return y;
        }
        return -1;
    }

    public override float Begin(Random rng)
    {
        _rig.UseSet(SantaRig.Ks.Length - 1);
        var roofArt = _rig.ArtAt(SantaRig.Ks.Length - 1)!;
        _sky = _rig.Size;
        _sR = roofArt.S;
        _feetR = roofArt.StandFeet; _runnerR = roofArt.RunnerFeet;                 // measured off the roof-size pictures' pixels
        _u = _sc.U;
        _cx = _sc.CabinFoot.X;
        float wallW = _sc.CabinWall.Width;
        float guess = _sc.CabinFoot.Y - _sc.CabinWall.Height - _sc.CabinRoofRise;
        _ridgeL = _cx - 0.36f * wallW; _ridgeR = _cx + 0.36f * wallW;

        // ---- The roof's top edge, from the pixels: the first snow row going down, at a few x clear of the chimney (median of them). ----
        var rows = new List<int>();
        foreach (float f in new[] { -0.30f, -0.18f, -0.06f, 0.06f, 0.16f })
        {
            int y = FirstSnowRow((int)(_cx + f * wallW), (int)(guess - 0.05f * _u), (int)(guess + 0.05f * _u));
            if (y >= 0) rows.Add(y);
        }
        rows.Sort();
        _ridgeY = rows.Count > 0 ? rows[rows.Count / 2] : guess;
        // ---- The chimney's rim: the first snow row of its cap, at its middle. ----
        int cap = FirstSnowRow((int)_sc.ChimneyTop.X, (int)(_sc.ChimneyTop.Y - 0.04f * _u), (int)(_sc.ChimneyTop.Y + 0.04f * _u));
        _rimX = _sc.ChimneyTop.X;
        _rimY = (cap >= 0 ? cap : _sc.ChimneyTop.Y) + 2f;                      // boots two pixels into the cap's snow

        _xTd = _ridgeR; _xEdge = _ridgeL;
        // THE LAYOUT, worked back from where the SLEIGH rests: its runners on the ridge, 2.25 roof s in from the left end; the rear
        // pair 1.45 s ahead of it on the ridge; the other three pairs and Rudolph beyond the eave, in the air.
        float xRestS = _ridgeL + RestSleighFromLeft * _sR;
        _rxF = xRestS - Off[SleighIdx] * _sR;                                     // where Rudolph ends up hovering
        for (int i = 0; i <= M; i++) _xRest[i] = _rxF + Off[i] * _sR;
        _rx0 = _sc.Width + 0.8f * _sky;                                           // where he starts: his nose just off the right edge
        _xShrink = _sc.Width;                                                     // the rig starts to shrink as Rudolph comes on screen
        // The sleigh touches down at the ridge's right end (its right end 0.9 s inside it) and skids to its rest while the team brakes.
        float db = MathF.Max(0.8f * _sR, _ridgeR - 0.9f * _sR - xRestS);          // the braking distance, the same for every part (the team is one rigid line)
        _v = (_rx0 - _rxF - db) / TApp;
        _tStop = 2f * db / _v;
        if (_tStop < TbMin)                                                       // too brisk a stop: brake over longer, and solve v and the distance together
        {
            _tStop = TbMin;
            db = (_tStop / 2f) * (_rx0 - _rxF) / (TApp + _tStop / 2f);
            _v = (_rx0 - _rxF - db) / TApp;
        }
        _xB = _rxF + db;                                                          // Rudolph begins to brake here
        _vmax = 1.95f * _v; _tAcc = 2.0f; _acc = _vmax / _tAcc;
        _hMax = 0.20f * _sc.Height;
        _xTdS = xRestS + db;
        _xStartS = _rx0 + Off[SleighIdx] * _sky;
        _hMaxS = _hMax * (_xStartS - _xTdS) / (_rx0 - _xTd) * 0.9f;
        _vLeap = 0.45f * _v;
        _tStopAll = TApp + _tStop + 0.4f;
        _lastPin = -1; _burst1 = _burst2 = _loud = false;

        // Santa's clip: everything below the chimney's rim, within its width, is hidden (so he goes IN).
        if (_clip == null || _clip.Length != _sc.OpenOfBranches.Length) _clip = new bool[_sc.OpenOfBranches.Length];
        Array.Copy(_sc.OpenOfBranches, _clip, _clip.Length);
        int w = _sc.Width, h = _sc.Height;
        int cutY = (int)_rimY + 1, x0 = (int)(_rimX - 0.0145f * _u), x1 = (int)(_rimX + 0.0145f * _u);
        for (int y = Math.Max(0, cutY); y < h; y++)
            for (int x = Math.Max(0, x0); x <= Math.Min(w - 1, x1); x++) _clip[y * w + x] = false;

        // ---- Scan the clock for the moments that depend on the curve (only the two that STAND on the roof touch it) ----
        for (int i = 0; i < M; i++)
        {
            _tH[i] = _tTd[i] = TDep; _tEdge[i] = 1e9f;
            if (!Stands(i)) continue;
            bool gotH = false, gotTd = false;
            for (float t = 0; t < TDep; t += 1f / 240f)
            {
                float x = XOf(i, t);
                float hh = Height(i, x, t);
                if (!gotH && hh < 0.22f * _sky * 0.7f) { _tH[i] = t; gotH = true; }
                if (!gotTd && hh < 0.5f) { _tTd[i] = t; gotTd = true; break; }
            }
            // The latest moment the pose can switch from the gallop to the landing reach: at the stretch of its own beat (phase 0.5).
            float phase = Frac(_tH[i] * Rate + PhaseOff[i]);
            _tSw[i] = _tH[i] + Frac(0.5f - phase) / Rate;
            for (float t = TDep; t < TDep + 12f; t += 1f / 240f)
                if (XOf(i, t) <= _xEdge) { _tEdge[i] = t; break; }
        }
        BuildGait();

        BuildEvents();
        _rig.Take();
        _rig.Mirror = true;
        _rig.ShowTraces = true;
        Place(0f);
        return TDep + 10.5f;                                                      // the nine take longer to clear the screen than the four did
    }

    private static float Frac(float x) => x - MathF.Floor(x);
    private static float Clamp01(float x) => x < 0 ? 0 : x > 1 ? 1 : x;
    private static float Smooth(float x) { x = Clamp01(x); return x * x * (3 - 2 * x); }
    private static float Lerp(float a, float b, float k) => a + (b - a) * k;

    // ==================================================================
    //  THE SIZE: perspective
    // ==================================================================

    /// <summary>How big the rig is at t, as a fraction of its flying size: 1 in the sky, 0.62 at the cabin, eased with Rudolph's progress.</summary>
    private float K(float t)
    {
        float kr = SantaRig.Ks[^1], rx = RX(t);
        if (t < TDep) return Lerp(1f, kr, Smooth((_xShrink - rx) / (_xShrink - _xTd)));
        return Lerp(kr, 1f, Smooth((_rxF - rx) / (0.65f * _u)));
    }

    /// <summary>The rung of the size ladder nearest to K(t) (K only moves one way at a time, so this never flickers).</summary>
    private int SetIdx(float t)
    {
        float k = K(t);
        int best = 0;
        for (int i = 1; i < SantaRig.Ks.Length; i++)
            if (MathF.Abs(SantaRig.Ks[i] - k) < MathF.Abs(SantaRig.Ks[best] - k)) best = i;
        return best;
    }

    /// <summary>The size unit in pixels at t, SMOOTH (not stepped): it lays out the spacing, so a change of rung (which only swaps pictures a few percent bigger or smaller) never moves a reindeer.</summary>
    private float S(float t) => _sky * K(t);

    // ==================================================================
    //  WHERE THE PARTS ARE AT t
    // ==================================================================

    /// <summary>Rudolph's x: a steady cruise, then a braking run along the ridge, a wait, and later a getaway that speeds up.</summary>
    private float RX(float t)
    {
        if (t < TDep)
        {
            if (t <= TApp) return _rx0 - _v * t;
            float tau = t - TApp;
            if (tau >= _tStop) return _rxF;
            return _xB - (_v * tau - 0.5f * (_v / _tStop) * tau * tau);
        }
        float d = t - TDep;
        if (d < _tAcc) return _rxF - 0.5f * _acc * d * d;
        return _rxF - 0.5f * _acc * _tAcc * _tAcc - _vmax * (d - _tAcc);
    }

    /// <summary>One part's x at t: Rudolph's x plus its own place behind him (the team is one line), and at the getaway the part starts a moment after the one in front.</summary>
    private float XOf(int i, float t) => RX(t < TDep ? t : t - Delay(i)) + Off[i] * S(t);

    /// <summary>The climb away: height gained after "dx" pixels of travel from the start. Quadratic at first (it leaves the roof slowly, ground effect), then a steady, steepening climb.</summary>
    private float Climb(float dx)
    {
        if (dx <= 0f) return 0f;
        float e = dx / _sky;
        return _sky * (0.40f * e * e / (e + 0.8f) + 0.02f * e * e);
    }

    /// <summary>The hovering team's lift above the ridge for a given x: nothing over the roof, rising smoothly past the eave to LiftMax (a gentle slope up toward Rudolph).</summary>
    private float Lift(float x) => LiftMax * _sR * Smooth((_ridgeL + 0.5f * _sR - x) / (4.5f * _sR));

    /// <summary>
    /// The curve, as a height above the roof for member i (the sleigh is member 9) standing at x at time t. On the way in it is a smooth
    /// bowl that comes in flat at the roof's end (so a touchdown is a glide-in, not a plunge); beyond the eave the hovering members
    /// ride up the lift slope; on the way out each part climbs after it has travelled, smoothly, from where it stood.
    /// (Its pixel sizes do not depend on the rig's current rung, so a change of rung never moves it.)
    /// </summary>
    private float Height(int i, float x, float t)
    {
        // ONE formula at every t (a formula that switched at the getaway made the slope jump for a frame): the descent's bowl, the
        // hover slope, and the climb, each zero where it does not apply.
        bool sleigh = i == SleighIdx;
        float td = sleigh ? _xTdS : _xTd, start = sleigh ? _xStartS : _rx0, top = sleigh ? _hMaxS : _hMax;
        float hgt = 0f;
        if (x > td)
        {
            float k = (x - td) / (start - td);
            hgt = top * k * k;
            hgt += 0.12f * _sR * MathF.Sin(x * 0.011f) * Clamp01(hgt / (0.8f * _sR));
        }
        float x0 = MathF.Min(_xRest[i], _xEdge);                  // the roof's own parts start to climb at the eave; the ones already in the air at once
        return hgt + (sleigh ? 0f : Lift(x)) + Climb(x0 - x);
    }

    /// <summary>The angle of the curve at x (nose down positive), from two points a little ahead and behind.</summary>
    private float Slope(int i, float x, float t, float reach)
    {
        float e = reach * S(t);
        return MathF.Atan2(Height(i, x + e, t) - Height(i, x - e, t), 2f * e);
    }

    private float SpeedOf(int i, float t)
    {
        float a = XOf(i, t), b = XOf(i, t - 0.04f);
        return MathF.Max(0f, (b - a) / 0.04f);
    }

    /// <summary>A standing reindeer's centre y at t: its hooves one pixel into the painted snow of the ridge.</summary>
    private float RoofC(float t) => _ridgeY + 1f - _feetR * K(t) / SantaRig.Ks[^1];      // smooth with the size: exact at the roof's size, where it matters

    private void SleighAt(float t, out float x, out float y, out float tilt)
    {
        x = XOf(SleighIdx, t);
        // The runners rest on the ridge's snow (measured: the lowest painted row of the sleigh's picture), no hovering.
        y = _ridgeY + 1f - _runnerR * K(t) / SantaRig.Ks[^1] - Height(SleighIdx, x, t);
        tilt = Slope(SleighIdx, x, t, 0.9f);
    }

    /// <summary>How much a hovering member bobs at t: 0 in flight and while standing, 1 once the team has settled into its hover, easing out as the getaway begins.</summary>
    private float HoverWeight(float t)
    {
        float inn = Smooth((t - (TApp + 0.4f * _tStop)) / 1.8f);
        return inn * (1f - Smooth((t - TDep) / 0.9f));
    }

    /// <summary>
    /// The hover members' beat, worked out once in Begin. Phase adds up speed (the gallop slows into a slow tread and speeds up again at the
    /// getaway without a hitch), and the gait steps down through three pictures (gallop, 0.72, 0.45 of the reach) at the GATHER of the
    /// member's own beat, where two gaits differ by under three pixels, and back up again at the getaway.
    /// </summary>
    private void BuildGait()
    {
        int n = (int)((TDep + 14f) * PhiHz) + 2;
        if (_phi.Length != M) _phi = new float[M][];
        for (int i = 0; i < M; i++)
        {
            var phi = _phi[i] != null && _phi[i].Length == n ? _phi[i] : new float[n];
            _phi[i] = phi;
            var list = new List<(float, int)> { (0f, 0) };
            _lv[i] = list;
            float p = 0f; int lvl = 0;
            for (int k = 0; k < n; k++)
            {
                float t = k / (float)PhiHz;
                float sf = Clamp01(SpeedOf(i, t) / _v);
                phi[k] = p;
                p += Rate * (HoverRate + (1f - HoverRate) * sf) / PhiHz;
                if (Stands(i)) continue;
                int target = sf > 0.75f ? 0 : sf > 0.40f ? 1 : 2;
                if (target != lvl && Frac(p + PhaseOff[i]) * SantaArt.FlyPages < 1f) { lvl = target; list.Add((t, lvl)); }
            }
        }
    }

    private float PhiAt(int i, float t)
    {
        float f = Math.Clamp(t, 0f, (_phi[i].Length - 2) / (float)PhiHz) * PhiHz;
        int k = (int)f; float r = f - k;
        return _phi[i][k] + (_phi[i][k + 1] - _phi[i][k]) * r;
    }

    /// <summary>Which book and page a reindeer shows at t (the gallop, the landing reach, the dip of the head, the paw, the look back; for the hovering ones the three gaits).</summary>
    private void Pose(int i, float t, out int book, out float page)
    {
        if (!Stands(i))
        {
            int lvl = 0;
            foreach (var (tt, l) in _lv[i]) { if (tt <= t) lvl = l; else break; }
            book = lvl == 0 ? 0 : lvl == 1 ? 5 : 6;
            page = Frac(PhiAt(i, t) + PhaseOff[i]) * SantaArt.FlyPages + 0.3f;
            return;
        }
        if (t < TDep)
        {
            if (t < _tSw[i]) { book = 0; page = Frac(t * Rate + PhaseOff[i]) * SantaArt.FlyPages + 0.3f; return; }
            if (t < _tStopAll)
            {
                book = 1;
                page = (SantaArt.LandPages - 1) * (1f - Clamp01(SpeedOf(i, t) / (_v * 0.9f)));
                return;
            }
            StandScript(i, t, out book, out page);
            return;
        }
        if (t < _tEdge[i])
        {
            book = 1;
            page = (SantaArt.LandPages - 1) * (1f - Clamp01(SpeedOf(i, t) / _vLeap));
            return;
        }
        book = 0;
        page = Frac(0.5f + (t - _tEdge[i]) * Rate) * SantaArt.FlyPages + 0.3f;
    }

    /// <summary>How far a back reindeer's head is turned to look at Santa at the chimney, 0 (forward) to 1 (looking behind), eased in and out.</summary>
    private static float LookBack(int i, float t)
    {
        float d = 0.15f * (i - 7);
        float a = Smooth((t - THop - d) / 0.5f) * (1f - Smooth((t - (TIn + 1.2f) - d) / 0.5f));            // while he climbs to the chimney and goes in
        float b = Smooth((t - (TOut - 0.5f) - d) / 0.5f) * (1f - Smooth((t - (THopBack + 0.6f) - d) / 0.5f));   // and while he comes back up
        return MathF.Max(a, b);
    }

    /// <summary>What a reindeer does while it waits on the roof: the front pair dip their heads and paw the snow in rounds; the back pair sniff and nod, and look round at Santa. (A standing animal is never still.)</summary>
    private void StandScript(int i, float t, out int book, out float page)
    {
        book = 3; page = 0;
        if (t > TDep - 1.6f) return;                              // quiet before the getaway
        if (i == M - 1)                                           // the far one of the rear pair paws; the near one sniffs and looks round
        {
            float t0 = _tStopAll + 0.3f + 0.7f * 0, c = t - t0;
            if (c < 0) return;
            c %= 11.5f;
            if (c < 0.6f) { page = (SantaArt.DipPages - 1) * Smooth(c / 0.6f); return; }
            if (c < 3.6f) { book = 2; page = ((c - 0.6f) * SantaArt.PawPages) % SantaArt.PawPages; return; }
            if (c < 4.2f) { page = (SantaArt.DipPages - 1) * (1f - Smooth((c - 3.6f) / 0.6f)); return; }
            return;
        }
        float look = LookBack(i, t);
        if (look > 0.02f) { book = 4; page = look * (SantaArt.LookPages - 1) + 0.02f; return; }
        float n = (t - _tStopAll - 1.0f) % 7.3f;
        if (n >= 0f && n < 2.4f) page = 2.2f * MathF.Pow(MathF.Sin(MathF.PI * n / 2.4f), 2f);
    }

    private TeamMember Member(int i, float t)
    {
        float x = XOf(i, t);
        TeamMember m = default;
        m.X = x;
        m.Y = RoofC(t) + DyStory[i] * S(t) - Height(i, x, t);
        m.Pitch = Slope(i, x, t, 0.9f) * 0.9f;
        Pose(i, t, out m.Book, out m.Page);
        if (!Stands(i))
        {
            // Hovering: each bobs a little, out of step with the rest, in proportion to how far out in the air it is (a slow rise and fall of about three seconds).
            float inAir = Clamp01(Lift(x) / (0.35f * LiftMax * _sR));
            m.Y -= 0.07f * S(t) * HoverWeight(t) * inAir * MathF.Sin(MathF.Tau * (t / 3.1f) + 1.9f * i);
            // Rudolph leans in just before the getaway: nose down, then up again as the team gets moving.
            if (i == 0) m.Pitch += 0.15f * Smooth((t - (TDep - 0.8f)) / 0.7f) * (1f - Smooth((t - (TDep + 0.5f)) / 0.9f));
        }
        return m;
    }

    // ==================================================================
    //  THE EVENTS: puffs of snow, soot and breath, each placed in time by Begin
    // ==================================================================

    private void BuildEvents()
    {
        var list = new List<Ev>();
        var rnd = new Random(1234);
        float R() => (float)rnd.NextDouble();
        void Add(float t0, float x, float y, float vx, float vy, float life, byte kind) => list.Add(new Ev { T0 = t0, X = x, Y = y, Vx = vx, Vy = vy, Life = life, Kind = kind });
        void Puff(float t0, float x, float y, int n, float power, bool sparks)
        {
            float sc = S(t0);
            for (int k = 0; k < n; k++)
            {
                float a = -MathF.PI * (0.1f + 0.8f * R());
                float sp = power * (0.4f + 0.6f * R()) * _u;
                Add(t0, x + (R() - 0.5f) * 0.1f * sc, y, MathF.Cos(a) * sp, MathF.Sin(a) * sp, 0.55f + 0.5f * R(), R() < 0.4f ? KSnowBig : KSnow);
            }
            if (sparks) for (int k = 0; k < 3; k++) Add(t0 + 0.05f * k, x + (R() - 0.5f) * 0.3f * sc, y - R() * 0.2f * sc, (R() - 0.5f) * 0.1f * _u, -0.12f * _u * R(), 0.6f + 0.3f * R(), KSpark);
        }

        // Touchdowns and the braking run: a puff of snow where each reindeer's hooves first meet the roof, and little skids after.
        for (int i = M - 2; i < M; i++)                                           // the rear pair, the only reindeer on the roof
        {
            float tt = _tTd[i];
            float x = XOf(i, tt);
            Puff(tt, x - 0.15f * S(tt), _ridgeY, 8, 0.20f, true);
            for (int k = 1; k <= 3; k++)
            {
                float ts = tt + (_tStopAll - tt) * k / 4f;
                Puff(ts, XOf(i, ts), _ridgeY, 3, 0.10f, false);
            }
        }
        // The sleigh sliding in: a little stardust off its runners as they meet the snow.
        {
            // The runners meet the snow at the moment the braking begins (TApp): a puff of snow and stardust, then skids as it slows.
            SleighAt(TApp, out float tx, out _, out _);
            Puff(TApp, tx + 0.5f * _sR, _ridgeY, 10, 0.22f, true);
            for (int k = 1; k <= 5; k++)
            {
                float ts = TApp + _tStop * k / 6f;
                SleighAt(ts, out float sx, out _, out _);
                Puff(ts, sx + 0.55f * _sR, _ridgeY, 4, 0.10f, false);
                Add(ts, sx + (R() - 0.5f) * 1.2f * _sR, _ridgeY - 0.1f * _sR, (R() - 0.5f) * 0.05f * _u, -0.03f * _u, 0.9f, KSpark);
            }
        }
        // Pawing: three flicks of snow from the hoof on every scrape of the front pair.
        for (int i = M - 1; i < M; i++)
            for (int round = 0; round < 3; round++)
            {
                float t0 = _tStopAll + 0.3f + round * 11.5f;
                if (t0 + 4.2f > TDep - 1.6f) break;
                float hx = XOf(i, t0 + 1f) - 0.24f * _sR;
                for (int cyc = 0; cyc < 3; cyc++)
                    for (int k = 0; k < 3; k++)
                        Add(t0 + 0.6f + cyc + 0.62f + 0.05f * k, hx, _ridgeY, (-0.02f - 0.05f * R()) * _u, (-0.10f - 0.10f * R()) * _u, 0.45f + 0.2f * R(), KSnow);
            }
        // Breath: a little vapour from each nose every few seconds while standing (it is cold).
        for (int i = 0; i < M; i++)
            for (int k = 0; ; k++)
            {
                float tb = _tStopAll + 0.8f + 2.9f * k + 0.43f * i;
                if (tb > TDep - 0.4f) break;
                TeamMember m = Member(i, tb);
                ReinPage pg = _rig.PageOf(m, false);
                Add(tb, m.X - pg.NoseX, m.Y + pg.NoseY, -0.012f * _u, -0.02f * _u, 1.3f, KVapour);
                Add(tb + 0.25f, m.X - pg.NoseX, m.Y + pg.NoseY, -0.014f * _u, -0.018f * _u, 1.1f, KVapour);
            }
        // Down the chimney (soot puff), up again (two), and the pats.
        Puff2(list, TIn - 0.1f, 9, rnd);
        Puff2(list, TOut - 0.2f, 6, rnd);
        for (int k = 0; k < 4; k++) Puff2(list, TDust + 0.3f * k + 0.1f, 5, rnd, true);
        // The getaway: snow kicked up behind each reindeer as it runs, and a burst where it leaves the roof.
        for (int i = M - 2; i < M; i++)
        {
            float te = _tEdge[i];
            if (te > 1e8f) continue;
            for (int k = 0; k < 4; k++)
            {
                float ts = te - 0.65f + 0.15f * k;
                if (ts < TDep) continue;
                Puff(ts, XOf(i, ts) + 0.15f * S(ts), _ridgeY, 4, 0.14f, false);
            }
            Puff(te, XOf(i, te), _ridgeY, 8, 0.24f, true);
        }
        // The sleigh's getaway: it slides along the ridge dragged by the line (snow kicked up behind its runners all the way), and lifts off at the eave in a burst.
        for (float ts = TDep + Delay(SleighIdx) + 0.15f; ts < TDep + 6f; ts += 0.28f)
        {
            SleighAt(ts, out float sx, out float sy, out _);
            if (sy < _ridgeY - 0.9f * _sR && ts > TDep + 1f) break;                // it has left the roof
            Puff(ts, sx + 0.6f * _sR, _ridgeY, 3, 0.12f, ts > TDep + 1.0f);
        }
        list.Sort((a, b) => a.T0.CompareTo(b.T0));
        _ev = [.. list];
    }

    /// <summary>Soot rising from the chimney's mouth (or from Santa's coat when "coat" is true: it falls a little as it leaves him).</summary>
    private void Puff2(List<Ev> list, float t0, int n, Random rnd, bool coat = false)
    {
        for (int k = 0; k < n; k++)
        {
            float R() => (float)rnd.NextDouble();
            float x = _rimX + (R() - 0.5f) * (coat ? 0.4f : 0.5f) * _sR, y = coat ? _rimY - 0.55f * _sR + R() * 0.3f * _sR : _rimY - 0.05f * _sR;
            list.Add(new Ev { T0 = t0 + 0.05f * R(), X = x, Y = y, Vx = (R() - 0.5f) * 0.10f * _u, Vy = (coat ? -0.04f : -0.14f - 0.10f * R()) * _u, Life = 0.9f + 0.7f * R(), Kind = KSoot });
        }
    }

    // ==================================================================
    //  UPDATE: put the rig where the script says
    // ==================================================================

    /// <summary>Sets the rig's size rung and parts for story time t.</summary>
    private void Place(float t)
    {
        _rig.UseSet(SetIdx(t));
        for (int i = 0; i < M; i++) _rig.Team[i] = Member(i, t);
        SleighAt(t, out _sx, out _sy, out _sTilt);
        _rig.SleighX = _sx; _rig.SleighY = _sy; _rig.SleighTilt = _sTilt;
        _rig.Pose = SleighPose(t);
        _rig.Mirror = true;
        // The gang line: slack and taut with the beat while flying (each stretch a little after the one in front); in the hover it is TAUT
        // back to the sleigh with a slow breath; at the getaway each stretch snaps taut pair by pair, Rudolph's first.
        for (int k = 0; k < 5; k++)
        {
            float tn;
            if (t < TApp) tn = 0.5f + 0.5f * MathF.Cos(MathF.Tau * (Frac(t * Rate + 0.5f) - 0.045f * k));
            else
            {
                float hov = 0.86f + 0.07f * MathF.Sin(t * 1.3f + 1.1f * k);
                float flight = 0.5f + 0.5f * MathF.Cos(MathF.Tau * (Frac(t * Rate + 0.5f) - 0.045f * k));
                tn = Lerp(flight, hov, HoverWeight(t) > 0f || t < TDep ? Smooth((t - TApp) / 1.5f) : 0f);
                float tt = TDep - 0.5f + 0.2f * k;                          // Rudolph's stretch first, the sleigh's last
                if (t >= TDep - 0.8f) tn = Lerp(tn, 1f, Smooth((t - tt) / 0.35f));
                if (t >= TDep + 3f) tn = Lerp(tn, flight, Smooth((t - TDep - 3f) / 1.5f));
            }
            _rig.LineTension[k] = tn;
        }
        _rig.Tension = _rig.LineTension[0];
        _rig.DustRate = t < _tStopAll ? 70f : t < TDep ? 10f : (t < TDep + 5f ? 150f : 70f);
    }

    private SantaArt.SleighPose SleighPose(float t)
    {
        if (t >= TRise && t < TSit) return SantaArt.SleighPose.EmptyNoSack;
        if (t < 1.5f) return SantaArt.SleighPose.Sit;
        if (t < 4.0f) return ((int)(t * 3.3f) & 1) == 0 ? SantaArt.SleighPose.WaveA : SantaArt.SleighPose.WaveB;         // he waves at the cabin
        if (t >= 4.6f && t < 6.8f) return SantaArt.SleighPose.Down;                                                        // and looks down at the roof
        if (t >= TWave && t < TWave + 1.1f) return ((int)((t - TWave) * 3.3f) & 1) == 0 ? SantaArt.SleighPose.WaveA : SantaArt.SleighPose.WaveB;
        if (t >= TCrack && t < TCrack + 0.2f) return SantaArt.SleighPose.CrackUp;
        if (t >= TCrack + 0.2f && t < TCrack + 0.55f) return SantaArt.SleighPose.CrackDown;
        return SantaArt.SleighPose.Sit;
    }

    public override void Update(float t)
    {
        Place(t);
        // The animals watch: Movement at the roof once a second; one Loud at the landing.
        int sec = (int)t;
        PointF roof = new(_cx, _ridgeY);
        if (sec != _lastPin && t > 3f) { _lastPin = sec; _w.Board.Post(Sense.Movement, roof, 1.0f * _u, 0.6f, _rig); }
        if (!_loud && t >= _tTd[M - 1]) { _loud = true; _w.Board.Post(Sense.Loud, roof, 0.5f * _u, 0.5f, _rig); }
        // A sparkle burst when the sack refills as he sits, and one for the getaway.
        if (!_burst1 && t >= TSit) { _burst1 = true; _rig.Burst(_sx - 0.45f * _sR, _sy - 0.2f * _sR, 18, 0.16f * _u); }
        if (!_burst2 && t >= TDep + 0.3f) { _burst2 = true; _rig.Burst(_sx, _sy + 0.3f * _sR, 28, 0.22f * _u); }
    }

    public override void End()
    {
        _rig.ShowTraces = true;
        _rig.DustRate = 70f;
        _rig.Release();
    }

    // ==================================================================
    //  DRAW
    // ==================================================================

    private enum Where { None, InSleigh, Outside, OnRim }

    public override void Draw(FrameBuffer fb, float t)
    {
        DrawWindows(fb, t);

        // Santa standing inside the sleigh is drawn BEFORE it, so the sleigh's side hides his legs.
        Where where = Figure(t, out SantaFigure.Page? page, out float fx, out float fy, out bool mirror);
        var fig = _rig.ArtAt(SantaRig.Ks.Length - 1)!.Figure;
        if (where == Where.InSleigh && page != null) DrawFigure(fb, fig, page, fx, fy, mirror, _sc.OpenOfBranches);

        _rig.DrawRig(fb, _sc.OpenOfBranches, false);

        if (where is Where.Outside or Where.OnRim && page != null)
            DrawFigure(fb, fig, page, fx, fy, mirror, where == Where.OnRim ? _clip! : _sc.OpenOfBranches);

        DrawEvents(fb, t);
    }

    private void DrawFigure(FrameBuffer fb, SantaFigure fig, SantaFigure.Page page, float fx, float fy, bool mirror, bool[] stencil)
    {
        int left = (int)MathF.Round(fx) - (mirror ? page.Img.Width - 1 - fig.FeetX : fig.FeetX), top = (int)MathF.Round(fy) - fig.FeetY;
        page.Img.Draw(fb, left, top, 1f, stencil, mirror);
    }

    /// <summary>
    /// Santa on foot at t: which page, where his boots are, which way he faces (mirror = facing left),
    /// and whether he is inside the sleigh (drawn behind its side), out in the open, or at the chimney
    /// (drawn through the clip). All at the roof size: he is only ever on foot there.
    /// </summary>
    private Where Figure(float t, out SantaFigure.Page? page, out float x, out float y, out bool mirror)
    {
        var fig = _rig.ArtAt(SantaRig.Ks.Length - 1)!.Figure;
        page = null; x = y = 0; mirror = true;
        if (t < TRise || t >= TSit) return Where.None;
        float s = _sR;
        SleighAt(t, out float sx, out float sy, out _);
        float floorY = sy + 0.30f * s, rearX = sx + 0.42f * s;
        PointF rim = new(_rimX, _rimY);

        if (t < TTurn1)                                           // springs to his feet, facing the way the team faces
        {
            float k = (t - TRise) / 0.45f;
            page = k >= 1f ? fig.StandSack : fig.Rise[Math.Clamp((int)(k * 3), 0, 2)];
            x = sx; y = floorY; return Where.InSleigh;
        }
        if (t < TWalk)                                            // turns round to face the chimney: left, front, right
        {
            float u = (t - TTurn1) / (TWalk - TTurn1);
            page = u < 0.28f ? fig.StandSack : u < 0.72f ? fig.FrontSack : fig.StandSack;
            mirror = u < 0.28f;
            x = sx; y = floorY; return Where.InSleigh;
        }
        mirror = false;
        if (t < THop)                                             // climbs to the back of the sleigh
        {
            float k = Clamp01((t - TWalk) / (THop - 0.3f - TWalk));
            float dist = (rearX - sx) * Smooth(k);
            page = k >= 1f ? fig.StandSack : fig.WalkSack[((int)(dist / (0.30f * s) * 8f) % 8 + 8) % 8];
            x = sx + dist; y = floorY; return Where.InSleigh;
        }
        if (t < THop + 0.75f)                                     // the hop
        {
            float u = t - THop;
            SleighAt(THop, out float hx, out float hy, out _);
            float x0 = hx + 0.42f * s, y0 = hy + 0.30f * s;
            int pi = u < 0.15f ? 0 : u < 0.30f ? 1 : u < 0.55f ? 2 : u < 0.65f ? 3 : 4;
            float k = Smooth((u - 0.2f) / 0.45f);
            x = Lerp(x0, rim.X, k);
            y = Lerp(y0, rim.Y, k) - 4f * k * (1 - k) * 0.55f * s;
            if (u < 0.2f) y = y0 + 0.02f * s * (u < 0.15f ? u / 0.15f : 1f);
            if (u >= 0.65f) { x = rim.X; y = rim.Y + (u < 0.7f ? 0.02f * s : 0f); }
            page = fig.HopSack[pi]; return Where.Outside;
        }
        if (t < TLift)                                            // on the rim, looking down the chimney
        {
            page = fig.StandSack; x = rim.X; y = rim.Y; return Where.OnRim;
        }
        if (t < TIn)                                              // down he goes
        {
            float u = t - TLift;
            float depth;                                           // how far below the rim his boots are, in s
            float wiggle = 0f;
            if (u < 0.8f) depth = 0.58f * Smooth(u / 0.8f);
            else if (u < 1.6f) { float k = (u - 0.8f) / 0.8f; depth = 0.58f + 0.05f * k + 0.03f * MathF.Sin(k * 20f); wiggle = 0.03f * MathF.Sin(k * 26f) * (1 - k * 0.3f); }
            else depth = 0.63f + 1.35f * Smooth((u - 1.6f) / 0.5f) * Smooth((u - 1.6f) / 0.5f);
            page = fig.SlideSack; x = rim.X + wiggle * s; y = rim.Y + depth * s; return Where.OnRim;
        }
        if (t < TOut) return Where.None;
        if (t < TOut + 1.4f)                                       // up again, hat first
        {
            float u = (t - TOut) / 1.4f;
            float depth = 1.98f * (1 - Smooth(u)) + 0.04f * MathF.Sin(u * 18f) * (1 - u);
            page = fig.PopSoot; x = rim.X; y = rim.Y + depth * s; return Where.OnRim;
        }
        if (t < TDust)
        {
            float shake = 0.012f * MathF.Sin((t - TOut) * 40f);
            page = fig.StandSoot; x = rim.X + shake * s; y = rim.Y; return Where.OnRim;
        }
        if (t < TTurn)
        {
            page = t < TDust + 1.2f ? fig.Pat[Math.Clamp((int)((t - TDust) / 0.3f), 0, 3)] : fig.Stand;
            x = rim.X; y = rim.Y; return Where.OnRim;
        }
        if (t < THopBack)                                          // turns round: right, front, left
        {
            float u = (t - TTurn) / (THopBack - TTurn);
            page = u < 0.28f ? fig.Stand : u < 0.72f ? fig.Front : fig.Stand;
            mirror = u >= 0.72f;
            x = rim.X; y = rim.Y; return Where.OnRim;
        }
        mirror = true;
        if (t < TWalkBack)                                         // the hop back into the sleigh, at its back
        {
            float u = t - THopBack;
            SleighAt(TWalkBack, out float hx, out float hy, out _);
            float x1 = hx + 0.42f * s, y1 = hy + 0.30f * s;
            int pi = u < 0.15f ? 0 : u < 0.30f ? 1 : u < 0.55f ? 2 : u < 0.65f ? 3 : 4;
            float k = Smooth((u - 0.2f) / 0.45f);
            x = Lerp(rim.X, x1, k);
            y = Lerp(rim.Y, y1, k) - 4f * k * (1 - k) * 0.55f * s;
            if (u < 0.2f) y = rim.Y + 0.02f * s * (u < 0.15f ? u / 0.15f : 1f);
            if (u >= 0.65f) { x = x1; y = y1; }
            page = fig.HopNo[pi]; return u >= 0.65f ? Where.InSleigh : Where.Outside;
        }
        if (t < TSitStart)                                         // walks forward to the seat
        {
            float k = Clamp01((t - TWalkBack) / (TSitStart - 0.2f - TWalkBack));
            float dist = (rearX - sx) * Smooth(k);
            page = k >= 1f ? fig.Stand : fig.WalkNo[((int)(dist / (0.30f * s) * 8f) % 8 + 8) % 8];
            x = rearX - dist; y = floorY; return Where.InSleigh;
        }
        {
            float k = (t - TSitStart) / (TSit - TSitStart);                              // sits down
            page = fig.RiseNo[Math.Clamp((int)((1 - k) * 3), 0, 2)];
            x = sx; y = floorY; return Where.InSleigh;
        }
    }

    // ---- The cabin's windows ----

    private void DrawWindows(FrameBuffer fb, float t)
    {
        RectangleF[] win = _sc.CabinWindows;
        // The right window first, then the left: each brightens and flickers, and stays bright until he is on his way out.
        for (int i = 0; i < 2; i++)
        {
            float on = Smooth((t - (TIn - 0.5f + 0.9f * i)) / 0.5f) * (1f - Smooth((t - (TSit - 1.2f + 0.4f * i)) / 1.2f));
            if (on <= 0.01f) continue;
            float flick = 0.6f + 0.2f * MathF.Sin(t * 17f + i * 2f) + 0.12f * MathF.Sin(t * 41f + i) + 0.08f * MathF.Sin(t * 5.3f);
            _winGlow.DrawCentered(fb, win[i].X + win[i].Width / 2, win[i].Y + win[i].Height / 2, on * flick);
        }
        // The family's tree, in the left window: its lights blink.
        if (t > TIn + 0.9f && t < TSit - 1.5f)
        {
            RectangleF w0 = win[0];
            float wx = w0.X + w0.Width / 2;
            for (int k = 0; k < 5; k++)
            {
                float ty = 0.35f + 0.13f * k, half = w0.Width * 0.26f * (ty - 0.12f);
                float px = wx + half * (k % 2 == 0 ? -0.6f : 0.6f), py = w0.Y + w0.Height * ty;
                float blink = MathF.Sin(t * 6.5f + k * 1.9f) > 0.1f ? 1f : 0.25f;
                _treeGlow[k % 3].DrawCentered(fb, px, py, 0.9f * blink * Smooth((t - TIn - 0.9f) / 0.8f));
            }
        }
        // A shadow crosses the right window, right to left: Santa on his rounds, with his sack. It is the
        // silhouette of his own walking pages, shown from the belly up (the sill hides the rest).
        float tShadow = TIn + 1.9f; const float Dur = 2.2f;
        if (t >= tShadow && t < tShadow + Dur)
        {
            var fig = _rig.ArtAt(SantaRig.Ks.Length - 1)!.Figure;
            RectangleF w1 = win[1];
            float u = (t - tShadow) / Dur;
            float figW = fig.W * 0.5f;
            float feetX = w1.Right + figW * 0.45f - u * (w1.Width + figW * 0.9f);
            float feetY = w1.Bottom + 0.30f * _sR;
            int pi = ((int)(u * 9f) % 8 + 8) % 8;
            Silhouette(fb, fig.WalkSack[pi].Img, feetX, feetY, fig, true, w1, 0.82f);
        }
    }

    /// <summary>Paints a picture's shape in a dark ink where it falls inside "clip" (a window): a shadow behind the glass.</summary>
    private static void Silhouette(FrameBuffer fb, Sprite sp, float feetX, float feetY, SantaFigure fig, bool mirror, RectangleF clip, float alpha)
    {
        int left = (int)MathF.Round(feetX) - (mirror ? sp.Width - 1 - fig.FeetX : fig.FeetX), top = (int)MathF.Round(feetY) - fig.FeetY;
        int cx0 = (int)MathF.Max(0, MathF.Floor(clip.X)), cx1 = (int)MathF.Min(fb.Width, MathF.Ceiling(clip.Right));
        int cy0 = (int)MathF.Max(0, MathF.Floor(clip.Y)), cy1 = (int)MathF.Min(fb.Height, MathF.Ceiling(clip.Bottom));
        ReadOnlySpan<uint> src = sp.Argb;
        for (int y = Math.Max(cy0, top); y < Math.Min(cy1, top + sp.Height); y++)
            for (int x = Math.Max(cx0, left); x < Math.Min(cx1, left + sp.Width); x++)
            {
                int sx = x - left, sy = y - top;
                uint c = src[sy * sp.Width + (mirror ? sp.Width - 1 - sx : sx)];
                int a = (int)(c >> 24);
                if (a < 40) continue;
                fb.Pixels[y * fb.Width + x] = FrameBuffer.Blend(fb.Pixels[y * fb.Width + x], 26, 18, 34, alpha * a / 255f);
            }
    }

    // ---- Puffs ----

    private void DrawEvents(FrameBuffer fb, float t)
    {
        bool[] stencil = _sc.OpenOfBranches;
        for (int i = 0; i < _ev.Length; i++)
        {
            ref readonly Ev e = ref _ev[i];
            float age = t - e.T0;
            if (age < 0) break;                                    // sorted by start time: none after this one has begun
            if (age >= e.Life) continue;
            float k = age / e.Life;
            float drag = 1f - 0.35f * k;
            float gy = e.Kind == KSnow || e.Kind == KSnowBig ? 0.55f * _u : e.Kind == KSpark ? 0.06f * _u : -0.03f * _u;
            float px = e.X + e.Vx * age * drag, py = e.Y + e.Vy * age * drag + 0.5f * gy * age * age;
            float a = MathF.Pow(1 - k, 1.3f);
            switch (e.Kind)
            {
                case KSnow: _snow.DrawCentered(fb, px, py, a * 0.95f, stencil); break;
                case KSnowBig: _snowBig.DrawCentered(fb, px, py, a * 0.8f, stencil); break;
                case KSpark: _spark.DrawCentered(fb, px, py, a * (0.6f + 0.4f * MathF.Sin(age * 22f)), stencil); break;
                case KSoot: _soot.DrawCentered(fb, px, py, a * 0.75f, stencil); break;
                default: _vapour.DrawCentered(fb, px, py, a * 0.7f, stencil); break;
            }
        }
    }
}
