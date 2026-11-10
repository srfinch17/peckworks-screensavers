using Peckworks.Screensavers.Core;
using Christmas.Happenings;

namespace Christmas;

/// <summary>One reindeer of the team, as the rig draws it: where its body centre is, how it is tilted, and which flip-book page.</summary>
internal struct TeamMember
{
    public float X, Y;          // the middle of its body, in pixels
    public float Pitch;         // radians, nose down positive (the nearest drawn tilt is a page, the rest a smooth shear)
    public int Book;            // 0 = flying gallop, 1 = landing (reach to stand), 2 = pawing, 3 = standing with the head dipping, 4 = looking back, 5 = mid gait (0.72 of the reach), 6 = hover gait (0.45)
    public float Page;          // the page in that book (the whole part counts; a gallop wraps)
    public int PitchIdx;        // unused since the tilt became page plus shear (kept so a story that sets it still builds)
}

/// <summary>
/// SANTA: the sleigh, the team of NINE (four pairs, two abreast, and Rudolph alone in front), and the flight
/// across the sky. (Moved out of ChristmasScene.cs on 2026-11-10 so that a story can take
/// the reins; rebuilt the same day as a rig of separate parts; the team grew from five to nine the same evening.)
///
/// THE PARTS. SantaArt.cs paints every picture once: each reindeer is its own flip-book of
/// a flying gallop (20 pages, jointed legs), the sleigh is a set of pages (Santa seated,
/// waving, looking down, cracking the reins) at five tilts. Here each frame the rig puts
/// each part where the PATH says, on its own beat, and joins them with lines (the gang line, the tugs
/// and the reins) that sag when the team eases and tighten when it stretches. The nine reindeer SHARE
/// one set of pictures (the far one of a pair is the same picture tinted darker and bluer at draw time).
///
/// THE PATH. A gentle curve: a straight line across the sky (along the top, or a long
/// diagonal that crosses the moon) with two slow waves added, so the team rises and dips.
/// Every part is placed on the SAME curve a fixed distance behind the front (the
/// reindeer behind Rudolph follow the path he took, like carriages behind an engine, or beads on a string:
/// each takes the path of the one in front), and is tilted along its own bit of the curve. The sleigh's tilt is the slope of
/// the curve under it, eased, and its height is a damped spring after the path's height: it lags a rise and
/// overshoots a little on a dip.
///
/// POSITION IS AN ADDED-UP DISTANCE (_d), never "time times speed": a story can take over the
/// reins (Take), move the parts by hand, and give them back (Release).
///
/// SMOOTHNESS (the owner begged for it): the beat is 20 pages at 1.5 a second, so at 60 frames a second every
/// page is held exactly two frames (the old 16 pages at 1.55 held them 2,2,3,2,2,3: a judder); a reindeer's tilt is
/// the nearest of three drawn pages plus a SHEAR at draw time (StampShear), so a tilt that drifts never pops, and the
/// stamp places a picture to a fraction of a pixel, so a bob is a glide.
///
/// He is only stamped onto sky pixels (the painter hands over a stencil that says which
/// pixels are sky), so he passes behind the branches that reach in from the top corners,
/// and in front of the moon and stars. Where he crosses the MOON he turns into a
/// silhouette (the sprite is drawn twice, once on the sky pixels outside the moon and
/// once, darkened, on the moon's pixels): the classic picture.
///
/// Test variables: PECKWORKS_SANTA=moon (every flight crosses the moon), =left (every
/// flight goes right to left); PECKWORKS_SHEET=santa;file.png writes the pose sheet;
/// PECKWORKS_MINDLOG logs the rig (every part's place, page and mode, each frame).
/// </summary>
internal sealed class SantaRig
{
    // ---- The team's layout along the path, in s units from the FRONT point D (forward is positive; dy is down).
    //      NINE reindeer: member 0 is Rudolph; pair p (1 to 4) is member 2p-1 (the NEAR one, drawn nearer the eye) and 2p (the FAR
    //      one). Seen from the side a pair is two reindeer side by side, so the far one is drawn a little behind (0.22 s, a third
    //      of a body length) and a little above (0.12 s) the near one: both heads and both racks show, and the far one is a shade
    //      darker and bluer (farther off, at night). Pairs are 1.3 s apart (a reindeer is about 1 s long at full stretch), so the
    //      whole rig, nose to the sleigh's tail, is about 8.2 s: a quarter of the screen width at 1080p. ----
    public const int Members = 9;
    public static readonly float[] Dx = [0f, -1.55f, -1.77f, -2.85f, -3.07f, -4.15f, -4.37f, -5.45f, -5.67f];
    public static readonly float[] Dy = [0.05f, 0.10f, -0.02f, 0.10f, -0.02f, 0.10f, -0.02f, 0.10f, -0.02f];
    /// <summary>Each reindeer's own place in the beat (golden-ratio fractions: no two are in step, and a pair's two are about 0.38 of a beat apart).</summary>
    public static readonly float[] PhaseOff = [0f, 0.618f, 0.236f, 0.854f, 0.472f, 0.090f, 0.708f, 0.326f, 0.944f];
    public const float SleighBack = -6.9f;                                       // the sleigh's centre, behind the front point (s)
    private const float GaitRate = 1.5f;                                         // gallop beats a second: 20 pages at 1.5 is 30 pages a second, two frames a page at 60
    private const int Motes = 72;                                                // the stardust ring buffer

    private readonly ChristmasScenery _scenery;
    private readonly float _s, _u;
    private readonly int _w, _h;
    private readonly Random _rng;
    private readonly StreamWriter? _log = MindLog.Open();
    private readonly bool _quickStory;
    private readonly string _force;
    /// <summary>
    /// THE SIZE LADDER. The cabin is farther away than the sky lane, so a story shrinks the whole rig as it flies
    /// down to the roof (and grows it back on the way out). Every size is its own set of pictures (nothing is
    /// ever scaled per frame), and the rungs are at most 8 percent apart, so a switch is a small step.
    /// Rung 0 is the flight's size; rung 6 is the roof size and carries everything, Santa on foot included.
    /// </summary>
    public static readonly float[] Ks = [1f, 0.92f, 0.85f, 0.78f, 0.72f, 0.66f, 0.62f];
    private readonly SantaArt?[] _ladder = new SantaArt?[Ks.Length];
    private int _ladderState;                       // 0 the middle rungs are not built, 1 building (a helper thread), 2 built
    private int _set;
    private readonly int _moonL, _moonT, _moonR, _moonB;                          // the moon's box and disc: a picture drawn over it goes dark (a silhouette)
    private readonly float _moonX, _moonY, _moonR2;
    private Sprite[] _noseGlow = [], _noseCore = [], _noseSparkle = [];
    private Sprite? _gold, _white, _star, _red;

    private double _time, _gait;
    private bool _flying;
    private double _pause = 9.0, _d, _len, _p0x, _p0y, _tx, _ty, _nx, _ny, _a1, _l1, _ph1, _a2, _l2, _ph2, _speed, _dir;
    private double _nextEvent = 3.0, _evStart, _evEnd;
    private int _evKind;                                                          // 0 none, 1 wave, 2 look down, 3 crack the reins
    /// <summary>How taut the traces are, 0 (hanging slack) to 1 (straight). The flight beats it with the gallop; a story sets LineTension by hand.</summary>
    public float Tension = 0.5f;
    /// <summary>How taut each stretch of the gang line is (0 slack, 1 straight): 0 Rudolph to pair 1, 1 pair 1 to 2, 2 pair 2 to 3, 3 pair 3 to 4, 4 pair 4 to the sleigh. The flight sets all five from the beat (each a little after the one in front); a story sets them by hand.</summary>
    public readonly float[] LineTension = [0.5f, 0.5f, 0.5f, 0.5f, 0.5f];
    private int _sleighIdx = SantaArt.Tilts / 2;
    private float _sleighRes;                       // the part of the sleigh's tilt the drawn page does not carry (a shear at draw time, as a slope)
    private double _sprY, _sprV; private bool _sprInit;                           // the sleigh's own height: a spring after the path (lag and a little overshoot on the dips)
    private readonly float[] _prevX = new float[Members + 1], _prevY = new float[Members + 1];
    private readonly PointF[] _ring = new PointF[Members], _mid = new PointF[5];

    // ---- What a story may set when it has the reins (Directed). Flight sets the same things from the path. ----
    public readonly TeamMember[] Team = new TeamMember[Members];
    public float SleighX, SleighY, SleighTilt;
    public SantaArt.SleighPose Pose;
    public bool Mirror;                                                           // true: flying to the left
    public bool ShowReins = true, ShowTraces = true;
    /// <summary>The sleigh's hand and hitch, the nose and so on (in pixels from the page centre) as drawn this frame, for a story to line things up.</summary>
    public bool Directed { get; private set; }
    public bool Ready => _ladder[0] != null;
    /// <summary>True when every rung of the size ladder is painted (a story waits for it).</summary>
    public bool LadderReady => Volatile.Read(ref _ladderState) == 2;

    /// <summary>
    /// The middle rungs of the size ladder (five sets, most of the pictures' memory) are only needed while a story
    /// flies the rig to the cabin, so they are painted on a helper thread when a story first asks, and let go
    /// when it ends. The flight's own size (rung 0) and the roof size (rung 6) are always there.
    /// Returns true once they are built.
    /// </summary>
    public bool EnsureLadder()
    {
        if (LadderReady) return true;
        if (Interlocked.CompareExchange(ref _ladderState, 1, 0) == 0) Task.Run(BuildMiddleRungs);
        return false;
    }

    private void BuildMiddleRungs()
    {
        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            for (int i = Ks.Length - 2; i >= 1; i--) _ladder[i] = new SantaArt(_s * Ks[i], full: false, figure: false);
            Volatile.Write(ref _ladderState, 2);
            if (_log != null)
            {
                long bytes = 0;
                foreach (var a in _ladder) bytes += a?.Bytes ?? 0;
                _log.WriteLine(FormattableString.Invariant($"0.000;Santa;art;middle rungs built in {sw.ElapsedMilliseconds} ms, {bytes / 1048576.0:F1} MB of pictures in all"));
            }
        }
        catch (Exception ex) { _log?.WriteLine("SantaArt middle rungs failed: " + ex); Volatile.Write(ref _ladderState, 0); }
    }

    /// <summary>Lets the middle rungs go (a story has ended): their memory is given back.</summary>
    public void ReleaseLadder()
    {
        if (!LadderReady) return;
        _set = 0;
        for (int i = 1; i < Ks.Length - 1; i++) _ladder[i] = null;
        Volatile.Write(ref _ladderState, 0);
    }
    /// <summary>The pictures at the size in use now (see UseSet).</summary>
    public SantaArt? Art => _ladder[_set];
    public SantaArt? ArtAt(int rung) => _ladder[rung];
    /// <summary>Chooses the rung of the size ladder the rig is drawn at from now on.</summary>
    public void UseSet(int rung) { if (_ladder[rung] != null) _set = rung; }
    /// <summary>The flight's size unit (rung 0), in pixels.</summary>
    public float Size => _s;

    // ---- stardust ----
    private readonly float[] _mx = new float[Motes], _my = new float[Motes], _mvx = new float[Motes], _mvy = new float[Motes], _mage = new float[Motes], _mlife = new float[Motes];
    private readonly byte[] _mkind = new byte[Motes];
    private int _mhead;
    private float _emit, _emitRed;
    /// <summary>Stardust per second falling from the runners (a story turns it up for a burst).</summary>
    public float DustRate = 70f;

    public SantaRig(ChristmasScenery scenery)
    {
        _scenery = scenery;
        _w = scenery.Width; _h = scenery.Height; _u = scenery.U;
        _s = _u * 0.058f;
        _rng = new Random(HappeningDirector.SceneRandom().Next());
        _force = Environment.GetEnvironmentVariable("PECKWORKS_SANTA") ?? "";
        _quickStory = Environment.GetEnvironmentVariable("PECKWORKS_STORY") == "1";
        if (_quickStory) _pause = 3600;       // a test that asks for the story at 4 s must find him off screen
        if (_force == "off") _pause = 1e9;    // test: no flights at all (the "without me" timing)

        // The moon's disc, for the silhouette: no stencil arrays, just its middle and radius (the stamp tests each pixel it draws).
        var moon = scenery.Moon;
        _moonX = moon.At.X; _moonY = moon.At.Y; _moonR2 = moon.R * moon.R;
        _moonL = (int)(moon.At.X - moon.R - 2); _moonR = (int)(moon.At.X + moon.R + 2);
        _moonT = (int)(moon.At.Y - moon.R - 2); _moonB = (int)(moon.At.Y + moon.R + 2);

        _noseGlow = new Sprite[Ks.Length]; _noseSparkle = new Sprite[Ks.Length]; _noseCore = new Sprite[Ks.Length];
        for (int k = 0; k < Ks.Length; k++)
        {
            float sk = _s * Ks[k];
            _noseGlow[k] = Sprite.Glow(Math.Max(3, (int)(sk * 0.45f)), Color.FromArgb(255, 48, 40));
            _noseSparkle[k] = PaintSparkle(Math.Max(8, (int)(sk * 0.9f)));
            int core = Math.Max(3, (int)(sk * 0.11f));
            _noseCore[k] = Sprite.Paint(core, core, g =>
            {
                using var b = new SolidBrush(Color.FromArgb(255, 60, 52));
                using var hot = new SolidBrush(Color.FromArgb(255, 255, 190, 170));
                g.FillEllipse(b, 0, 0, core - 0.5f, core - 0.5f);
                float q = core * 0.25f;
                g.FillEllipse(hot, q, q * 0.8f, q, q);
            });
        }
        _gold = Sprite.Glow(Math.Max(2, (int)(_u * 0.0065f)), Color.FromArgb(255, 214, 110));
        _white = Sprite.Glow(Math.Max(2, (int)(_u * 0.0045f)), Color.FromArgb(255, 252, 240));
        _red = Sprite.Glow(Math.Max(2, (int)(_u * 0.004f)), Color.FromArgb(255, 70, 60));
        _star = PaintSparkle(Math.Max(5, (int)(_u * 0.016f)));

        // The pictures take a moment to paint: a helper thread does it while the snow is already falling.
        Task.Run(() =>
        {
            try
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                // The flight's size first (the flights need only that), then the roof size, then the rungs between.
                _ladder[0] = new SantaArt(_s, full: true, figure: false);
                int roof = Ks.Length - 1;
                _ladder[roof] = new SantaArt(_s * Ks[roof], full: true, figure: true);
                string? sheet = Environment.GetEnvironmentVariable("PECKWORKS_SHEET");
                if (sheet != null && sheet.StartsWith("santa;", StringComparison.OrdinalIgnoreCase))
                {
                    _ladder[0]!.WriteSheet(sheet[6..]);
                    string roofSheet = Path.Combine(Path.GetDirectoryName(sheet[6..]) ?? ".", Path.GetFileNameWithoutExtension(sheet[6..]) + "_roof.png");
                    _ladder[roof]!.WriteSheet(roofSheet);
                }
                if (_log != null) _log.WriteLine(FormattableString.Invariant($"0.000;Santa;art;flight and roof sizes built in {sw.ElapsedMilliseconds} ms, {(_ladder[0]!.Bytes + _ladder[roof]!.Bytes) / 1048576.0:F1} MB (flight size {_ladder[0]!.Bytes / 1048576.0:F1} MB, roof size {_ladder[roof]!.Bytes / 1048576.0:F1} MB)"));
                // A test that asks for the story at 4 s needs the rest ready by then; a real run waits until a story asks.
                if (_quickStory && Interlocked.CompareExchange(ref _ladderState, 1, 0) == 0) BuildMiddleRungs();
            }
            catch (Exception ex) { _log?.WriteLine("SantaArt failed: " + ex); }
        });
    }

    // ==================================================================
    //  THE FLIGHT
    // ==================================================================

    /// <summary>Hands the reins to a story: the rig stops flying and draws whatever the story sets.</summary>
    public void Take() { Directed = true; _flying = false; }

    /// <summary>Takes the reins back. He is off screen by now; the next flight starts after a short pause.</summary>
    public void Release() { ReleaseLadder(); Directed = false; _flying = false; _set = 0; _pause = _quickStory ? 3600 : 40 + 50 * _rng.NextDouble(); }

    /// <summary>The wobble the path adds to the straight line: two slow waves, so the team rises and dips.</summary>
    private double Wave(double d) => _a1 * Math.Sin(Math.Tau * d / _l1 + _ph1) + _a2 * Math.Sin(Math.Tau * d / _l2 + _ph2);

    private void PathAt(double d, out float x, out float y)
    {
        double off = Wave(d);
        x = (float)(_p0x + _tx * d + _nx * off);
        y = (float)(_p0y + _ty * d + _ny * off);
    }

    /// <summary>The path's slope at d, as the angle of travel (nose down positive), from two points a little apart.</summary>
    private float AngleAt(double d)
    {
        double e = _s * 0.45;
        PathAt(d + e, out float x1, out float y1); PathAt(d - e, out float x0, out float y0);
        return MathF.Atan2(y1 - y0, (x1 - x0) * (float)_dir);
    }

    /// <summary>Chooses the next flight and lays its path out.</summary>
    private void BeginFlight()
    {
        double r = _rng.NextDouble();
        int kind = _force == "moon" ? 2 : _force == "left" ? 1 : r < 0.40 ? 0 : r < 0.58 ? 1 : 2;       // 0 along the top, left to right; 1 along the top, right to left; 2 across the moon
        _dir = kind == 1 ? -1 : kind == 0 ? 1 : (_rng.Next(2) == 0 ? 1 : -1);
        double sx = _dir > 0 ? 0 : _w, ex = _dir > 0 ? _w : 0;
        var moon = _scenery.Moon;
        double y0, y1, yMoon = moon.At.Y;
        if (kind == 2)
        {
            double slope = (0.05 + 0.05 * _rng.NextDouble()) * (_rng.Next(2) == 0 ? 1 : -1);          // rises or falls across the sky
            yMoon = moon.At.Y + (_rng.NextDouble() - 0.5) * 0.7 * moon.R;
            y0 = yMoon + slope * (sx - moon.At.X); y1 = yMoon + slope * (ex - moon.At.X);
            _a1 = _u * (0.008 + 0.008 * _rng.NextDouble()); _a2 = _u * 0.004 * _rng.NextDouble();
        }
        else
        {
            y0 = _h * (0.085 + 0.065 * _rng.NextDouble());
            y1 = y0 + _h * (-0.025 + 0.075 * _rng.NextDouble());
            _a1 = _u * (0.016 + 0.014 * _rng.NextDouble()); _a2 = _u * (0.004 + 0.006 * _rng.NextDouble());
        }
        _l1 = _w * (0.45 + 0.25 * _rng.NextDouble()); _l2 = _w * (0.22 + 0.12 * _rng.NextDouble());
        _ph1 = _rng.NextDouble() * Math.Tau; _ph2 = _rng.NextDouble() * Math.Tau;
        _p0x = sx; _p0y = y0;
        double dx = ex - sx, dy = y1 - y0;
        _len = Math.Sqrt(dx * dx + dy * dy);
        _tx = dx / _len; _ty = dy / _len; _nx = -_ty; _ny = _tx;

        if (kind == 2)
        {
            // Aim: the point of the rig named by MoonFocus must pass over the moon's middle. Work out
            // where along the baseline that is, and slide the whole path up or down to hit it.
            double dc = (moon.At.X - _p0x) / _tx;
            PathAt(dc, out _, out float py);
            _p0y -= py - yMoon;                // the path's wave puts it off by "py - yMoon" there: slide the whole path back
        }
        _d = -0.8 * _s; _sprInit = false;
        _speed = _u * (0.082 + 0.012 * _rng.NextDouble());
        _flying = true;
        _evKind = 0; _nextEvent = _time + 2.0 + 3.0 * _rng.NextDouble();
        Mirror = _dir < 0;
        _log?.WriteLine(FormattableString.Invariant($"{_time:F2};Santa;flight;kind={kind};dir={_dir};len={_len:F0};speed={_speed:F1}"));
    }

    public void Update(float dt)
    {
        if (!Ready) { _scenery.SantaOnScreen = false; return; }
        long u0 = _log != null ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
        try { UpdateInner(dt); } finally { Meter(u0); }
        if (_log != null) WriteLog();                     // the log's own cost is kept out of the meter
        if (_log != null && ++_costFrames % 600 == 0)
        {
            double ms = 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            _log.WriteLine(FormattableString.Invariant($"{_time:F1};Santa;cost;rig update+draw average {_costTicks * ms / 600:F3} ms a frame over the last 600 frames (all frames, in view or not), {(_costViewFrames > 0 ? _costViewTicks * ms / _costViewFrames : 0):F3} ms a frame over the {_costViewFrames} frames with the rig drawn, worst single call {_costWorst * ms:F3} ms"));
            _costTicks = 0; _costWorst = 0; _costViewTicks = 0; _costViewFrames = 0;
        }
    }

    private void UpdateInner(float dt)
    {
        _time += dt;
        _gait = (_gait + dt * GaitRate) % 1.0;

        if (!Directed)
        {
            if (!_flying)
            {
                _pause -= dt;
                if (_pause <= 0) BeginFlight();
            }
            if (_flying)
            {
                _d += _speed * (1.0 + 0.04 * Math.Sin(_time * 0.9)) * dt;
                LayOutFlight(dt);
                if (_d - 7.9 * _s > _len) _flying = false;
                if (!_flying) _pause = _quickStory ? 3600 : 45 + 60 * _rng.NextDouble();   // SANTA IS AN EVENT (the first review: at 5 to 12 s he was on screen more than half the time, wallpaper, and the main reason every animal kept looking up)
            }
        }

        EmitDust(dt);
        UpdateFacts();
    }

    /// <summary>Puts the parts where the path says: each reindeer a fixed distance behind the front point, the sleigh behind them, tilted by the slope under it.</summary>
    private void LayOutFlight(float dt)
    {
        for (int i = 0; i < Members; i++)
        {
            // BEADS ON A STRING: every reindeer sits on the SAME curve a fixed distance behind Rudolph, so each takes the path of
            // the one in front, and is tilted along its own bit of it.
            PathAt(_d + Dx[i] * _s, out float x, out float y);   // the path's distance grows in the direction of travel, so "forward" is plus
            ref TeamMember m = ref Team[i];
            m.X = x; m.Y = y + Dy[i] * _s;
            m.Pitch = AngleAt(_d + Dx[i] * _s) * 0.9f;
            m.Book = 0;
            m.Page = (float)(((_gait + PhaseOff[i]) % 1.0) * SantaArt.FlyPages) + 0.3f;     // the 0.3 keeps a page change away from a whole number: no frame-rounding flicker
        }
        // The sleigh: its tilt eases toward the slope under it, and its height is a damped spring after the path's height
        // (it lags a few pixels behind a rise and overshoots a little on a dip, like a heavy thing on a line).
        PathAt(_d + SleighBack * _s, out float sx, out float sy);
        if (!_sprInit) { _sprY = sy; _sprV = 0; _sprInit = true; }
        const double Om = 5.5, Ze = 0.55;
        double sdt = Math.Min(dt, 0.05);
        _sprV += (Om * Om * (sy - _sprY) - 2 * Ze * Om * _sprV) * sdt;
        _sprY += _sprV * sdt;
        SleighX = sx; SleighY = (float)_sprY;
        float target = AngleAt(_d + SleighBack * _s);
        float k = 1f - MathF.Exp(-dt * 3.5f);
        SleighTilt += (target - SleighTilt) * k;

        // Poses: now and then he waves, looks down at the world, or cracks the reins (and the team surges).
        if (_evKind == 0 && _time >= _nextEvent)
        {
            double pick = _rng.NextDouble();
            _evKind = pick < 0.40 ? 1 : pick < 0.72 ? 2 : 3;
            _evStart = _time;
            _evEnd = _time + (_evKind == 1 ? 2.6 : _evKind == 2 ? 1.9 : 0.6);
        }
        Pose = SantaArt.SleighPose.Sit;
        if (_evKind != 0)
        {
            double a = _time - _evStart;
            if (_evKind == 1) Pose = ((int)(a * 3.3) & 1) == 0 ? SantaArt.SleighPose.WaveA : SantaArt.SleighPose.WaveB;
            else if (_evKind == 2) Pose = SantaArt.SleighPose.Down;
            else Pose = a < 0.3 ? SantaArt.SleighPose.CrackUp : SantaArt.SleighPose.CrackDown;
            if (_time >= _evEnd) { _evKind = 0; _nextEvent = _time + 3.5 + 5.0 * _rng.NextDouble(); }
        }
        // The lines: taut at the stretch of the front pair's beat, slack at the gather; a crack snaps them taut. Each stretch
        // of the gang line tightens a little after the one in front of it (the pull travels back down the line).
        Tension = 0.5f + 0.5f * MathF.Cos(MathF.Tau * (float)((_gait + PhaseOff[0] + 0.5) % 1.0));
        if (_evKind == 3) Tension = 1f;
        for (int j = 0; j < 5; j++)
            LineTension[j] = _evKind == 3 ? 1f : 0.5f + 0.5f * MathF.Cos(MathF.Tau * (float)((_gait + PhaseOff[0] + 0.5 - 0.045 * j + 1.0) % 1.0));
    }

    // ==================================================================
    //  FACTS FOR THE SCENE, THE STARDUST AND THE LOG
    // ==================================================================

    private void UpdateFacts()
    {
        float half = Art!.S * 0.9f;
        float minX = SleighX, maxX = SleighX, minY = SleighY, maxY = SleighY;
        foreach (TeamMember m in Team) { minX = MathF.Min(minX, m.X); maxX = MathF.Max(maxX, m.X); minY = MathF.Min(minY, m.Y); maxY = MathF.Max(maxY, m.Y); }
        bool on = (_flying || Directed) && maxX + half > 0 && minX - half < _w && maxY + half > 0 && minY - half < _h;
        _scenery.SantaOnScreen = on;
        _scenery.SantaSleigh = new PointF(SleighX, SleighY);
        PointF nose = NoseWorld();
        _scenery.SantaNose = nose;
    }

    /// <summary>Rudolph's nose in the world, from the page that is drawn (so the glow is on the muzzle in every beat of the gallop).</summary>
    public PointF NoseWorld()
    {
        if (Art == null) return default;
        ReinPage pg = PageOf(Team[0], false);
        return new PointF(Team[0].X + (Mirror ? -pg.NoseX : pg.NoseX), Team[0].Y + pg.NoseY + pg.NoseX * ShearOf(Team[0]));
    }

    internal ReinPage PageOf(TeamMember m, bool quantise)
    {
        var art = Art!;
        int page = (int)MathF.Floor(m.Page);
        switch (m.Book)
        {
            case 4: return art.Look[Math.Clamp(page, 0, SantaArt.LookPages - 1)];
            case 3: return art.Dip[Math.Clamp(page, 0, SantaArt.DipPages - 1)];
            case 1: return art.Land[Math.Clamp(page, 0, SantaArt.LandPages - 1)];
            case 2: return art.Paw[((page % SantaArt.PawPages) + SantaArt.PawPages) % SantaArt.PawPages];
            case 5: return art.Mid[((page % SantaArt.FlyPages) + SantaArt.FlyPages) % SantaArt.FlyPages];
            case 6: return art.Hover[((page % SantaArt.FlyPages) + SantaArt.FlyPages) % SantaArt.FlyPages];
            default:
                int half = SantaArt.Pitches / 2;
                int idx = Math.Clamp((int)MathF.Round(m.Pitch / SantaArt.PitchStep), -half, half) + half;
                return art.Fly[idx][((page % SantaArt.FlyPages) + SantaArt.FlyPages) % SantaArt.FlyPages];
        }
    }

    /// <summary>
    /// The tilt a reindeer's page does NOT carry, as a slope (tan of an angle). A gallop page is drawn at the nearest of
    /// three tilts; the rest (at most 0.12 rad) is a shear at draw time, so a tilt that drifts is smooth, never a pop.
    /// The other books are drawn level and take the whole tilt (held to a quarter radian).
    /// </summary>
    internal static float ShearOf(TeamMember m)
    {
        float res;
        if (m.Book == 0)
        {
            int half = SantaArt.Pitches / 2;
            int idx = Math.Clamp((int)MathF.Round(m.Pitch / SantaArt.PitchStep), -half, half);
            res = m.Pitch - idx * SantaArt.PitchStep;
        }
        else res = Math.Clamp(m.Pitch, -0.25f, 0.25f);
        return MathF.Tan(Math.Clamp(res, -0.3f, 0.3f));
    }

    /// <summary>The sleigh page nearest the tilt, and the shear that carries the rest.</summary>
    private void PickSleigh()
    {
        int half = SantaArt.Tilts / 2;
        int k = Math.Clamp((int)MathF.Round(SleighTilt / SantaArt.TiltStep), -half, half);
        _sleighIdx = k + half;
        _sleighRes = MathF.Tan(Math.Clamp(SleighTilt - k * SantaArt.TiltStep, -0.2f, 0.2f));
    }

    /// <summary>The sleigh picture for the pose and tilt in use (the middle rungs of the ladder paint only three poses: any other falls back to the seated one).</summary>
    private SleighPage SleighPageNow()
    {
        PickSleigh();
        var row = Art!.Sleigh[(int)Pose] ?? Art.Sleigh[0];
        return row[Math.Clamp(_sleighIdx, 0, SantaArt.Tilts - 1)];
    }

    private void EmitDust(float dt)
    {
        bool show = (_flying || Directed) && _scenery.SantaOnScreen;
        // Age and move what is in the air.
        for (int i = 0; i < Motes; i++)
        {
            if (_mage[i] >= _mlife[i]) continue;
            _mage[i] += dt;
            _mx[i] += _mvx[i] * dt; _my[i] += _mvy[i] * dt;
            _mvx[i] *= 1f - 0.9f * dt; _mvy[i] = _mvy[i] * (1f - 0.9f * dt) + _u * 0.05f * dt;       // drag, and a slow fall
        }
        if (!show) return;
        _emit += dt * DustRate;
        var page = SleighPageNow();
        float ox = SleighX + (Mirror ? -page.RunnerX : page.RunnerX), oy = SleighY + page.RunnerY + page.RunnerX * _sleighRes;
        while (_emit >= 1f)
        {
            _emit -= 1f;
            Spawn(ox + Jit(0.12f), oy + Jit(0.06f), -_dirOf() * Jit01(0.02f) * _u, Jit(0.03f) * _u, (byte)(_rng.Next(4) == 0 ? 2 : _rng.Next(2)), 0.9f + 1.0f * (float)_rng.NextDouble());
        }
        // Rudolph's nose sheds a few red sparks that drift back.
        _emitRed += dt * 7f;
        PointF nose = NoseWorld();
        while (_emitRed >= 1f)
        {
            _emitRed -= 1f;
            Spawn(nose.X + Jit(0.05f), nose.Y + Jit(0.05f), -_dirOf() * (0.02f + 0.03f * (float)_rng.NextDouble()) * _u, Jit(0.02f) * _u, 3, 0.5f + 0.5f * (float)_rng.NextDouble());
        }
    }

    private float _dirOf() => Mirror ? -1f : 1f;
    private float Jit(float amount) => (float)(_rng.NextDouble() * 2 - 1) * amount * _s;
    private static float Jit01(float amount) => amount;

    private void Spawn(float x, float y, float vx, float vy, byte kind, float life)
    {
        int i = _mhead; _mhead = (_mhead + 1) % Motes;
        _mx[i] = x; _my[i] = y; _mvx[i] = vx; _mvy[i] = vy; _mage[i] = 0; _mlife[i] = life; _mkind[i] = kind;
    }

    /// <summary>A burst of stardust at a point (a landing's puff, a take-off's flare), for a story.</summary>
    public void Burst(float x, float y, int count, float speed)
    {
        for (int i = 0; i < count; i++)
        {
            float a = (float)(_rng.NextDouble() * Math.Tau), v = speed * (0.3f + 0.7f * (float)_rng.NextDouble());
            Spawn(x, y, MathF.Cos(a) * v, MathF.Sin(a) * v - speed * 0.2f, (byte)(_rng.Next(3) == 0 ? 2 : _rng.Next(2)), 0.7f + 0.9f * (float)_rng.NextDouble());
        }
    }

    private void WriteLog()
    {
        string mode = Directed ? "directed" : _flying ? "flying" : "pause";
        for (int i = 0; i < Members; i++)
        {
            float sp = MathF.Sqrt((Team[i].X - _prevX[i]) * (Team[i].X - _prevX[i]) + (Team[i].Y - _prevY[i]) * (Team[i].Y - _prevY[i])) / MathF.Max(1e-4f, 1f / 60f);
            _log!.WriteLine(FormattableString.Invariant($"{_time:F3};Santa;{i};{Team[i].X:F3};{Team[i].Y:F3};{sp:F1};{mode};book{Team[i].Book};{Team[i].Page:F1};pitch={Team[i].Pitch:F4}"));
            _prevX[i] = Team[i].X; _prevY[i] = Team[i].Y;
        }
        float ss = MathF.Sqrt((SleighX - _prevX[Members]) * (SleighX - _prevX[Members]) + (SleighY - _prevY[Members]) * (SleighY - _prevY[Members])) * 60f;
        _log!.WriteLine(FormattableString.Invariant($"{_time:F3};Santa;sleigh;{SleighX:F3};{SleighY:F3};{ss:F1};{mode};{Pose};tilt={SleighTilt:F4}"));
        _prevX[Members] = SleighX; _prevY[Members] = SleighY;
    }

    // ==================================================================
    //  DRAWING
    // ==================================================================

    // Cost meter (only when PECKWORKS_MINDLOG is on): the rig's own time per frame, so its cost does not have to be dug out of the whole scene's noise.
    private long _costTicks, _costWorst, _costFrames, _costViewTicks, _costViewFrames;
    private void Meter(long start)
    {
        if (_log == null) return;
        long d = System.Diagnostics.Stopwatch.GetTimestamp() - start;
        _costTicks += d; if (d > _costWorst) _costWorst = d;
    }

    public void Draw(FrameBuffer fb)
    {
        if (Directed || !Ready || !_flying) return;
        long t0 = _log != null ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
        DrawRig(fb, _scenery.IsSky, true);
        if (_log != null)
        {
            long d = System.Diagnostics.Stopwatch.GetTimestamp() - t0;
            _costTicks += d; if (d > _costWorst) _costWorst = d;
            if (_scenery.SantaOnScreen) { _costViewTicks += d; _costViewFrames++; }
        }
    }

    /// <summary>
    /// The coat tints (red, green, blue multipliers out of 256). Rudolph a touch redder-brown, the near ones a little different
    /// from each other, so the team are not clones; the FAR one of each pair is its mate's tint dimmed and pushed toward blue
    /// (it is farther off, and it is night).
    /// </summary>
    private static readonly int[] TintR, TintG, TintB;
    static SantaRig()
    {
        int[] nr = [276, 250, 262, 238, 256], ng = [236, 238, 246, 236, 240], nb = [212, 232, 228, 244, 220];
        TintR = new int[Members]; TintG = new int[Members]; TintB = new int[Members];
        for (int i = 0; i < Members; i++)
        {
            int p = (i + 1) / 2; bool far = i > 0 && i % 2 == 0;
            TintR[i] = far ? nr[p] * 184 >> 8 : nr[p]; TintG[i] = far ? ng[p] * 206 >> 8 : ng[p]; TintB[i] = far ? nb[p] * 248 >> 8 : nb[p];
        }
    }
    private const int MoonShade = 62;                                              // out of 256: how dark a thing is where it crosses the moon

    /// <summary>
    /// Stamps one picture through a stencil, tinted (mr, mg, mb out of 256), SHEARED and placed to a fraction of a pixel,
    /// and over the MOON's disc darkened to a silhouette (each pixel it draws is asked whether it lies inside the disc).
    ///
    /// THE SHEAR. A plain stamp drops the picture on whole pixels: a bob of a pixel and a half is a hop of one or two, and a
    /// tilt that changes by a tenth of a radian swaps to a different page (a pop). Here each COLUMN of the picture is moved up
    /// or down by its own amount, "fy + (distance from the middle) * tan(tilt)", and a fraction of a pixel is shown by blending
    /// the column with itself moved one row (so a bob is a glide). A small rotation of a picture IS, to a good approximation, a
    /// shear of this kind (for 0.12 rad the difference is under a pixel). Where one of the two rows being blended is empty the
    /// other's colour is taken whole, so the edge never bleeds dark. Same blend as Sprite.Draw: result = old + (new - old) * alpha.
    /// </summary>
    private void StampShear(FrameBuffer fb, Sprite sp, int[] colFirst, int[] colLast, float cx, float cy, bool mirror, float tan, int mr, int mg, int mb, bool[] stencil)
    {
        int w = sp.Width, h = sp.Height, fw = fb.Width, fh = fb.Height;
        int left = (int)MathF.Round(cx) - w / 2;
        float topf = cy - h / 2f;
        int topi = (int)MathF.Floor(topf);
        float fy = topf - topi, reach = MathF.Abs(tan) * w * 0.5f + 2f;
        if (left >= fw || left + w <= 0 || topi + h + reach < 0 || topi - reach >= fh) return;
        bool moon = left < _moonR && left + w > _moonL && topi - reach < _moonB && topi + h + reach > _moonT;
        bool plain = mr == 256 && mg == 256 && mb == 256;
        ReadOnlySpan<uint> src = sp.Argb;
        uint[] frame = fb.Pixels;
        float half = w * 0.5f;
        for (int sx = 0; sx < w; sx++)
        {
            int f = colFirst[sx], l = colLast[sx];
            if (f > l) continue;
            int dcol = left + (mirror ? w - 1 - sx : sx);
            if ((uint)dcol >= (uint)fw) continue;
            float shift = fy + (sx + 0.5f - half) * tan;
            int si = (int)MathF.Floor(shift), k8 = (int)((shift - si) * 256f), j8 = 256 - k8;
            for (int r = f; r <= l + 1; r++)
            {
                int dy = topi + si + r;
                if ((uint)dy >= (uint)fh) continue;
                uint c0 = r < h ? src[r * w + sx] : 0u, c1 = r > 0 ? src[(r - 1) * w + sx] : 0u;
                int a0 = (int)(c0 >> 24), a1 = (int)(c1 >> 24);
                int a = (a0 * j8 + a1 * k8) >> 8;
                if (a == 0) continue;
                int di = dy * fw + dcol;
                if (!stencil[di]) continue;
                int cr, cg, cb;
                if (a1 == 0 || a0 == 0)
                {
                    uint c = a1 == 0 ? c0 : c1;
                    cr = (int)((c >> 16) & 0xFF); cg = (int)((c >> 8) & 0xFF); cb = (int)(c & 0xFF);
                }
                else
                {
                    cr = (int)((((c0 >> 16) & 0xFF) * (uint)j8 + ((c1 >> 16) & 0xFF) * (uint)k8) >> 8);
                    cg = (int)((((c0 >> 8) & 0xFF) * (uint)j8 + ((c1 >> 8) & 0xFF) * (uint)k8) >> 8);
                    cb = (int)(((c0 & 0xFF) * (uint)j8 + (c1 & 0xFF) * (uint)k8) >> 8);
                }
                if (!plain || moon)
                {
                    int tr = mr, tg = mg, tb = mb;
                    if (moon)
                    {
                        float ddx = dcol + 0.5f - _moonX, ddy = dy + 0.5f - _moonY;
                        if (ddx * ddx + ddy * ddy < _moonR2) { tr = tr * MoonShade >> 8; tg = tg * MoonShade >> 8; tb = tb * MoonShade >> 8; }
                    }
                    cr = Math.Min(255, cr * tr >> 8); cg = Math.Min(255, cg * tg >> 8); cb = Math.Min(255, cb * tb >> 8);
                }
                uint bg = frame[di];
                int br = (int)((bg >> 16) & 0xFF), bgn = (int)((bg >> 8) & 0xFF), bb = (int)(bg & 0xFF);
                frame[di] = (uint)(((br + ((cr - br) * a >> 8)) << 16) | ((bgn + ((cg - bgn) * a >> 8)) << 8) | (bb + ((cb - bb) * a >> 8)));
            }
        }
    }

    private void StampMember(FrameBuffer fb, int i, bool[] stencil)
    {
        TeamMember m = Team[i];
        ReinPage pg = PageOf(m, true);
        StampShear(fb, pg.Img, pg.ColFirst, pg.ColLast, m.X, m.Y, Mirror, ShearOf(m), TintR[i], TintG[i], TintB[i], stencil);
    }

    private static float Dist(PointF a, PointF b) => MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    /// <summary>Draws the whole rig (stardust, sleigh, gang line, team, the nose's shine) through "stencil". sky = true: through the sky stencil, with the moon silhouette.</summary>
    public void DrawRig(FrameBuffer fb, bool[] stencil, bool sky)
    {
        var art = Art;
        if (art == null) return;
        float dir = Mirror ? -1f : 1f;
        float s = art.S;                                                  // the size unit of the rung in use

        // 1. Stardust (behind the rig).
        for (int i = 0; i < Motes; i++)
        {
            if (_mage[i] >= _mlife[i]) continue;
            float k = _mage[i] / _mlife[i], a = (1 - k) * (1 - k) * (0.65f + 0.35f * MathF.Sin(_mage[i] * 19f + i));
            Sprite sp = _mkind[i] == 0 ? _gold! : _mkind[i] == 1 ? _white! : _mkind[i] == 2 ? _star! : _red!;
            sp.DrawCentered(fb, _mx[i], _my[i], a * (_mkind[i] == 2 ? 0.95f : 0.85f), stencil);
        }

        // 2. The sleigh.
        SleighPage sg = SleighPageNow();
        StampShear(fb, sg.Img, sg.ColFirst, sg.ColLast, SleighX, SleighY, Mirror, _sleighRes, 256, 256, 256, stencil);

        // 3. DRAW ORDER of the team, fixed: the FAR reindeer of every pair first (they are farther off), then the gang line and the
        //    tugs (so a line is over the far animals and under the near ones), then the NEAR reindeer from the back of the team to
        //    the front (each one's rump is over the nose behind it), then Rudolph last, alone in front.
        for (int i = Members - 1; i >= 2; i -= 2) StampMember(fb, i, stencil);

        // The harness rings, in the world (the tilt's shear carries the ring's height too), and the middle of each pair.
        for (int i = 0; i < Members; i++)
        {
            ReinPage pg = PageOf(Team[i], true);
            _ring[i] = new PointF(Team[i].X + dir * pg.HarX, Team[i].Y + pg.HarY + pg.HarX * ShearOf(Team[i]));
        }
        _mid[0] = _ring[0];
        for (int p = 1; p <= 4; p++) _mid[p] = new PointF((_ring[2 * p - 1].X + _ring[2 * p].X) * 0.5f, (_ring[2 * p - 1].Y + _ring[2 * p].Y) * 0.5f);

        float line = MathF.Max(1.1f, s * 0.02f);
        Color cord = Color.FromArgb(226, 184, 104), tug = Color.FromArgb(214, 170, 96);
        if (ShowTraces)
        {
            // THE GANG LINE runs down the middle of the team from the sleigh's hitch to Rudolph, between the pairs; each
            // reindeer's collar is tied to it by a short TUG line. Slack lines sag, taut ones straighten.
            PointF hitch = new(SleighX + dir * sg.HitchX, SleighY + sg.HitchY + sg.HitchX * _sleighRes);
            for (int k = 0; k < 4; k++) Cord(fb, _mid[k + 1], _mid[k], Sag(k, Dist(_mid[k], _mid[k + 1]), s), cord, line * 1.25f, stencil);
            Cord(fb, hitch, _mid[4], Sag(4, Dist(hitch, _mid[4]), s), cord, line * 1.25f, stencil);
            for (int p = 1; p <= 4; p++)
            {
                float tg = (0.01f + 0.05f * (1f - LineTension[p - 1])) * s;
                Cord(fb, _mid[p], _ring[2 * p - 1], tg, tug, line * 0.85f, stencil);
                Cord(fb, _mid[p], _ring[2 * p], tg, tug, line * 0.85f, stencil);
            }
        }
        if (ShowReins && Pose != SantaArt.SleighPose.EmptyNoSack && Pose != SantaArt.SleighPose.EmptySack)
        {
            PointF hand = new(SleighX + dir * sg.HandX, SleighY + sg.HandY + sg.HandX * _sleighRes);
            Cord(fb, hand, _mid[4], (0.03f + 0.10f * (1f - LineTension[4])) * s, Color.FromArgb(150, 104, 70), line, stencil);
        }

        for (int i = Members - 2; i >= 1; i -= 2) StampMember(fb, i, stencil);
        StampMember(fb, 0, stencil);

        // 5. Rudolph's nose. Its shine goes through the same stencil as everything else, pixel by pixel,
        //    so a twig in front of him hides exactly the part of the glow it covers. The nose sprites come in
        //    the size of the rung in use.
        PointF nose = NoseWorld();
        float pulse = 0.75f + 0.25f * MathF.Sin((float)(_time * 5 % Math.Tau));                // a steady warm throb
        _noseGlow[_set].DrawCentered(fb, nose.X, nose.Y, pulse, stencil);
        _noseCore[_set].DrawCentered(fb, nose.X, nose.Y, 1f, stencil);
        // The sparkle: sin() raised to a high power is near zero most of the time and spikes briefly: a glint every few seconds.
        float glint = MathF.Pow(MathF.Max(0, MathF.Sin((float)(_time * 2.1 % Math.Tau))), 8);
        _noseSparkle[_set].DrawCentered(fb, nose.X, nose.Y, glint, stencil);
    }

    /// <summary>How far a stretch of the gang line hangs below its straight line: more when slack, and more for a longer stretch.</summary>
    private float Sag(int seg, float len, float s) => (0.012f + 0.085f * (1f - LineTension[seg])) * MathF.Min(len, 2.2f * s) * 0.5f;

    /// <summary>A line from a to b that hangs in a curve: "sag" pixels lower in the middle (a parabola, in seven short straight pieces).</summary>
    private void Cord(FrameBuffer fb, PointF a, PointF b, float sag, Color c, float width, bool[] stencil)
    {
        PointF prev = a;
        for (int i = 1; i <= 7; i++)
        {
            float t = i / 7f;
            var p = new PointF(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t + sag * 4f * t * (1f - t));
            fb.Line(prev, p, c, 0.95f, width, stencil);
            prev = p;
        }
    }

    // ==================================================================
    //  SMALL PAINTERS
    // ==================================================================

    /// <summary>
    /// A four-pointed sparkle: two thin diamonds, one lying down and one
    /// standing up, crossing in the middle. Flashed briefly over the nose
    /// now and then, the way a shiny thing catches the light.
    /// </summary>
    public static Sprite PaintSparkle(int size)
    {
        size = Math.Max(8, size);
        float c = size / 2f, thin = Math.Max(1f, size * 0.035f);
        return Sprite.Paint(size, size, g =>
        {
            using var ray = new SolidBrush(Color.FromArgb(230, 255, 226, 220));
            g.FillPolygon(ray, [new PointF(0, c), new PointF(c, c - thin), new PointF(size, c), new PointF(c, c + thin)]);
            g.FillPolygon(ray, [new PointF(c, 0), new PointF(c + thin, c), new PointF(c, size), new PointF(c - thin, c)]);
        });
    }
}
