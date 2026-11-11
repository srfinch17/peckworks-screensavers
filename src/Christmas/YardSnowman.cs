using Peckworks.Screensavers.Core;
using Peckworks.Screensavers.Core.Minds;

namespace Christmas;

// ============================================================================
// THE YARD SNOWMAN (YardSnowman.cs): the audience.
//
// FEYNMAN VERSION. He stands in the near snow and he is alive in a quiet way:
// he never walks, but he WATCHES. His head and eyes follow whatever happens
// (Santa going over, a fox slipping by, a firework) and he reacts with his
// whole body: he jumps at a bang and his hat flies off, he tips the hat to
// Santa, he shivers, he catches a snowflake on his carrot and goes cross-eyed,
// he yawns, he nods off and wakes with a start, he waves at the cabin, and a
// little snow builds up on his hat and arms until he shakes it off.
//
// He is drawn live from parts (YardSnowmanArt.cs has the parts and why): three
// balls that can squash, a head whose face slides round the ball when it turns,
// stick arms bent at the elbow by a two-bone "inverse kinematics" (give the
// hand a place to be; the elbow finds itself), a hat with its own little spring
// so it bounces and lands askew, and a striped scarf that streams in the
// breeze.
//
// HOW IT MOVES. Every frame each doing writes a GOAL (where the head should
// look, how open the eyes are, the mouth's shape, where the hands go, how
// squashed the body is). The snowman never jumps to a goal: each channel EASES
// toward it at its own rate (an eye is quick, a hand slower, a head slowest).
// That is what makes him silky: two doings that disagree about the head simply
// hand the head from one goal to the next and it glides. Only a few things are
// set outright, on purpose: the jump's body (a hop is a parabola, not a glide)
// and a held hat (it is in the hand, so it goes where the hand goes).
//
// WHAT HE IS SOLID TO (the world law). He is a prop with a foot: nothing may
// stand where he stands (ChristmasScenery.CanStand already refuses his foot).
// He passes BEHIND the near props (the holly, the log, the drifts) through the
// depth picture, like everything else on the snowfield. He pins nothing.
//
// WHO IS IN REACH. He pins no notices, so nothing hears him.
// ============================================================================

/// <summary>
/// What he is doing. WHAT CAN CUT EACH ONE SHORT, and WHAT IT LEAVES (the pose is a goal, so every exit just lets the
/// arms and face ease back; the only things left behind are the hat's skew and the snow on him):
///   Idle      cut by anything (a doing is picked when its timer ends). Leaves nothing.
///   Watch     (a notice drew his eye) renewed by the same notice, cut by a Loud (to Jump) or Santa. Leaves nothing.
///   HatTip    (Santa overhead) NOT cut (a hand is on the hat). Ends with the hat set down, skew untouched.
///   FixHat    (the hat is askew) NOT cut. Ends with the skew at 0.
///   Scan      cut by any reaction. Leaves nothing.
///   Shiver    cut by any reaction. Leaves nothing.
///   Snowflake cut by any reaction; the flake just melts where it is. Leaves nothing.
///   Yawn      cut by any reaction. Leaves nothing.
///   Doze      cut only by a Loud or a Threat (a startle, then Jump or Watch); a mere Movement does not wake him.
///             Leaves the hat slid and askew (FixHat is the next business).
///   WaveCabin cut by any reaction. Leaves nothing.
///   Shake     NOT cut. Leaves the snow at 0.
///   Jump      NOT cut. Leaves the hat askew (FixHat is the next business) and the cooldown on.
///   Search    (no carrot) cut by any reaction, ends when the carrot is back. Leaves nothing.
/// </summary>
internal sealed class YardSnowman : Resident
{
    private enum Doing { Idle, Watch, HatTip, FixHat, Scan, Shiver, Snowflake, Yawn, Doze, WaveCabin, Shake, Jump, Search }

    /// <summary>The face he wears while watching.</summary>
    private enum Mood { Curious, Alert, Suspicious, Awe }

    // Everything a doing may ask the body to be, this frame. Each channel eases toward it (see Ease).
    private struct Goal
    {
        public float Yaw, Pitch, YawRate, PitchRate;      // the head: turn, nod, how fast to get there
        public bool YawDirect;                            // set the turn outright (a head shake is a rhythm, not a glide)
        public float Open, Narrow; public bool Happy;     // eyes: how open, how cross, upside-down U
        public float GazeX, GazeY, Cross;                 // where the glints look, and how far they pull toward the nose
        public float MCurve, MWidth, MRing, MRingR, MRingH, MWob, MLat;   // mouth: smile depth, width, "o" amount, its size and height, a chatter, where it sits on the face
        public float Brow, BrowAng, Blush;                // twig brows: raised, angled (negative cross, positive sad); pink cheeks
        public float Squash, Sink, Lean, Tremble, Lift;   // the body: squash, head sunk, upper body leaned, a shiver, off the ground
        public bool Direct;                               // Squash and Lift are set outright (a jump)
        public float HatDx, HatDy;                        // where the hat's spring wants it, from its rest place
        public PointF HandL, HandR;                       // the hands, from the shoulders
        public float SpreadL, SpreadR;                    // how far the twig fingers fan
        public float OscL, OscR, OscHz; public bool OscX; // a wave or a shake added on top of the eased hand
    }

    private int _count;
    private readonly SnowmanSize _z;
    private YardSnowmanArt? _art;
    private readonly PointF _foot;
    private readonly Mind<Doing> _mind = new(Doing.Idle);
    private readonly Attention _att;
    private readonly float _seed;
    private Goal _g;
    private double _doT;                                  // seconds since this doing began
    private float _nextRoll;                              // a short rest between pieces of business

    // ---- the eased state: what is actually on screen ----
    private float _yaw, _pitch, _open = 1f, _narrow, _gazeX, _gazeY, _cross;
    private float _mCurve = 0.22f, _mWidth = 0.5f, _mRing, _mRingR = 0.12f, _mRingH = 1f, _mWob, _mLat = -0.40f;
    private float _brow, _browAng, _blush = 0.3f;
    private float _squash, _sink, _lean, _tremble, _lift;
    private bool _happy;
    private readonly PointF[] _hand = new PointF[2];      // eased hand, from its shoulder (screen pixels)
    private readonly float[] _spread = new float[2];

    // ---- the body's layout this frame (Layout) ----
    private int _sqk; private float _sqf;                // the squash page and how far (0 to 1) toward the next: bodies are blended, never stepped
    private float _midX, _c0y, _c1y, _headX, _headY, _neckY;
    private readonly PointF[] _sh = new PointF[2], _elb = new PointF[2], _wr = new PointF[2];
    private int _near;

    // ---- blink, scarf, snow load ----
    private float _blinkLeft, _nextBlink = 2.5f;
    private double _scarfPh;
    private float _snow, _snowRate;
    private float _snowAtShake;

    // ---- the hat: its own spring, plus the hand's hold on it ----
    private float _hatX, _hatY, _hatVx, _hatVy, _hatA, _hatW, _hatSkew;
    private bool _hatHold, _hatTipMode;
    private float _hatLift, _hatHeldAng;
    private int _hatSide = 1;
    private float _hatCx, _hatCy;                         // where the hat's middle really is (last frame), for the hand to reach

    // ---- senses and business ----
    private PointF _watchPt;
    private Mood _mood;
    private PointF _lookPt;
    private float _lookLeft;
    private float _reactT, _reactLeft; private int _reactKind;
    private int _passes, _sinceTip;
    private bool _wantTip;
    private float _jumpCool, _waveCool, _dozeCool, _quietFor;
    private int _scanDir = 1, _armSide = 1;
    private float _emitAcc;
    private float _flakeX, _flakeY, _flakeA;
    private PointF _flakeStart;
    private bool _jumpLanded, _jumpHatKicked;
    private readonly string? _force;
    private bool _forced;
    private bool _noseTest, _noseGiven;
    private double _forceAt = 6.0;
    private float _lastDt;
    private float _vis = 1f;                         // 0 to 1: he fades in over the first 0.8 s the residents run (the scene starts everyone on one frame: no figure may appear in one frame)
    private bool _santaPass;                           // a Santa pass (one Watch or one hat tip) is in progress
    private double _born = -1;                          // the scene clock at his first Update: his entrance (waking up) is timed from it
    private const double WakeLen = 3.8;
    private readonly float[] _weights = new float[8];
    // ---- cost instruments: the slowest Update and DrawOne seen, written to the mind log with the doing and the frame number ----
    private long _frameNo;
    private double _worstUpdate, _worstDraw, _sumUpdate, _sumDraw;

    // ---- particles: a fixed pool, nothing is made or thrown away per frame ----
    private const int Pool = 96;
    private readonly float[] _pX = new float[Pool], _pY = new float[Pool], _pVx = new float[Pool], _pVy = new float[Pool], _pAge = new float[Pool], _pLife = new float[Pool], _pPh = new float[Pool];
    private readonly int[] _pKind = new int[Pool];       // 0 powder, 1 sparkle, 2 "z"

    public bool HasNose { get; private set; } = true;
    public int Count => _count;

    /// <summary>The tip of the carrot (where it would be if it is gone), in pixels: a hare steals it from here.</summary>
    public PointF NoseTip
    {
        get
        {
            float r = _z.R2, lat0 = -0.06f, len = 0.90f * r;
            float ax = 0f, ay = MathF.Sin(lat0) - 0.12f, az = MathF.Cos(lat0), al = MathF.Sqrt(ax * ax + ay * ay + az * az);
            YardSnowmanArt.FacePoint(0, lat0, _yaw, _pitch, r, out float bx, out float by, out _);
            // The tip is the base moved along the axis (turned and nodded the same way).
            float x1 = (ax / al) * MathF.Cos(_yaw) + (az / al) * MathF.Sin(_yaw), z1 = -(ax / al) * MathF.Sin(_yaw) + (az / al) * MathF.Cos(_yaw);
            float yUp = (ay / al) * MathF.Cos(_pitch) + z1 * MathF.Sin(_pitch);
            return new PointF(_headX + bx + x1 * len, _headY + by - yUp * len);
        }
    }

    /// <summary>The top of the hat (the middle of its crown's top), in pixels.</summary>
    public PointF HatTop
    {
        get
        {
            float a = _hatHold ? _hatHeldAng : _hatA, up = 1.15f * _z.R2;
            return new PointF(_hatCx + MathF.Sin(a) * up, _hatCy - MathF.Cos(a) * up);
        }
    }

    /// <summary>Where a hand is (-1 left arm, +1 right arm): the tip of the wrist, where a bird could perch, in pixels.</summary>
    public PointF ArmTip(int side) => _wr[side < 0 ? 0 : 1];

    /// <summary>A story points his gaze: head and eyes follow this place for this many seconds.</summary>
    public void LookAt(PointF where, float seconds) { _lookPt = where; _lookLeft = MathF.Max(_lookLeft, seconds); }

    /// <summary>The carrot is taken: he is startled, then he feels for it with a hand, and is sad.</summary>
    public void TakeNose()
    {
        if (!HasNose) return;
        HasNose = false;
        React(0);
        if (_art != null) for (int i = 0; i < 7; i++) Emit(0, NoseTip.X, NoseTip.Y, (Rng.NextSingle() - 0.5f) * _z.T * 0.5f, -_z.T * (0.2f + 0.3f * Rng.NextSingle()), 0.7f, Rng.Next(2));
    }

    /// <summary>The carrot is back: a pop of sparkles and a happy face.</summary>
    public void GiveNose()
    {
        if (HasNose) return;
        HasNose = true;
        React(1);
        if (_art != null) { PointF n = NoseTip; for (int i = 0; i < 6; i++) Emit(1, n.X, n.Y, (Rng.NextSingle() - 0.5f) * _z.T * 0.7f, -_z.T * (0.1f + 0.3f * Rng.NextSingle()), 1.0f, i & 1); }
    }

    /// <summary>Plays a face and a body for 2 seconds: 0 surprise, 1 delight, 2 cross.</summary>
    public void React(int kind) { _reactKind = Math.Clamp(kind, 0, 2); _reactT = 0; _reactLeft = 2f; }

    public YardSnowman(ChristmasScenery s, int count, Random sceneRng, Noticeboard board) : base(s, sceneRng, board)
    {
        _count = Math.Max(0, count);
        _open = 0.04f;                                   // asleep at the start: the wake-up opens his eyes (BaseGoal)
        MindLog.Open();                                  // open the shared log now (on the scene thread, before the first frame), not inside the first Update
        _foot = s.SnowmanFoot;
        _z = new SnowmanSize(s.SnowmanHeight);
        // A steady, curious snowman: hard to frighten, glad to watch.
        _att = new Attention(0.12f + 0.2f * Traits.Roll(Rng), 0.75f + 0.25f * Traits.Roll(Rng));
        _seed = Traits.Roll(Rng) * 9f;
        _snowRate = 1f / (70f + 70f * Traits.Roll(Rng));
        _snow = 0.05f;
        _nextRoll = Organic.Jitter(Rng, 5f);
        _watchPt = new PointF(_foot.X, _foot.Y);
        string? f = Environment.GetEnvironmentVariable("PECKWORKS_SNOWMAN");
        _force = string.IsNullOrWhiteSpace(f) ? null : f.Trim().ToLowerInvariant();
        if (_force == "off") _count = 0;                 // PECKWORKS_SNOWMAN=off: the scene without him (for timing)
        _noseTest = _force == "nonose";
        // PECKWORKS_SNOWMAN_AT moves the forced second (default 6): Santa holds his eye until about 22 s.
        if (double.TryParse(Environment.GetEnvironmentVariable("PECKWORKS_SNOWMAN_AT"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double at)) _forceAt = at;
        for (int i = 0; i < 2; i++) { _hand[i] = RestHand(i); _spread[i] = 0.45f; }
        if (_count > 0)
        {
            SnowmanSize z = _z;
            BuildInBackground(() =>
            {
                _art = new YardSnowmanArt(z);
                // Compile every method of the snowman NOW, on this helper thread. The first Update used to cost 9.5 ms
                // because .NET compiles a method the first time it runs; PrepareMethod compiles without running it.
                const System.Reflection.BindingFlags all = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly;
                foreach (Type ty in new[] { typeof(YardSnowman), typeof(YardSnowmanArt), typeof(Mind<Doing>), typeof(Attention), typeof(Cooldown), typeof(Dice), typeof(Turn), typeof(Organic), typeof(Noticeboard), typeof(Sprite) })
                    foreach (var m in ty.GetMethods(all))
                        try { if (!m.IsAbstract && !m.ContainsGenericParameters) System.Runtime.CompilerServices.RuntimeHelpers.PrepareMethod(m.MethodHandle); } catch { }
            });
        }
    }

    // ==================================================================
    //  THE SCENE'S CALLS
    // ==================================================================

    public override void Update(float dt)
    {
        long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
        UpdateInner(dt);
        _frameNo++;
        double ms = (System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        _sumUpdate += ms;
        if (_frameNo % 600 == 0 && MindLog.Open() is { } avg)
            avg.WriteLine($"{Board.Time:F3} Snowman 0 MEAN over 600 frames: update {_sumUpdate / 600:F3} ms, draw {_sumDraw / 600:F3} ms");
        if (_frameNo % 600 == 0) { _sumUpdate = 0; _sumDraw = 0; }
        if (ms > _worstUpdate && ms > 0.5 && MindLog.Open() is { } log)
        {
            _worstUpdate = ms;
            log.WriteLine($"{Board.Time:F3} Snowman 0 SLOW update {ms:F2} ms frame {_frameNo} doing {_mind.Doing}");
        }
    }

    private void UpdateInner(float dt)
    {
        if (_art == null) return;
        dt = Math.Clamp(dt, 0f, 0.05f);
        _lastDt = dt;
        if (_born < 0) _born = Board.Time;
        // Nothing may walk through him: his place on the snow is his bottom ball's width (the scene's list of who stands where).
        S.Occupy(this, 0, _foot.X, _foot.Y, _z.R0);
        _mind.Tick(dt); _doT += dt; _att.Tick(dt);
        _jumpCool -= dt; _waveCool -= dt; _dozeCool -= dt; _quietFor += dt;
        if (_lookLeft > 0) _lookLeft -= dt;
        if (_reactLeft > 0) { _reactLeft -= dt; _reactT += dt; }
        // A little snow settles on him, a flake at a time.
        _snow = MathF.Min(1f, _snow + _snowRate * dt * (Doing.Shake == _mind.Doing ? 0f : 1f));

        ForceTests();
        Senses();
        if (_mind.Over && Board.Time - _born >= WakeLen) Decide();
        if (_mind.Doing == Doing.Search && HasNose) _mind.End();

        BaseGoal();
        Act(dt);
        if (_reactLeft > 0) ReactGoal();
        Ease(dt);
        Blink(dt);
        Layout();
        ArmsAndHat(dt);
        Particles(dt);

        // The scarf streams on the same breeze the snow falls on (PetalField's gust formula), faster in a gust.
        double t = Board.Time;
        float wind = Math.Clamp(0.55f + 0.5f * (float)Math.Sin(t * 0.11) + 0.3f * (float)Math.Sin(t * 0.043 + 2), 0f, 1.4f);
        _scarfPh = (_scarfPh + dt * (0.7 + 0.8 * wind)) % 1.0;

        if (MindLog.Open() is { } log)
            log.WriteLine($"{t:F3} Snowman 0 {_foot.X:F1} {_foot.Y:F1} 0 {_mind.Doing} yaw={_yaw:F3} pitch={_pitch:F3} open={_open:F2} sq={_squash:F2} lift={_lift:F1} " +
                          $"head={_headX:F1},{_headY:F1} hat={_hatCx:F1},{_hatCy:F1} hatA={(_hatHold ? _hatHeldAng : _hatA):F2} hold={(_hatHold ? 1 : 0)} " +
                          $"handL={_wr[0].X:F1},{_wr[0].Y:F1} handR={_wr[1].X:F1},{_wr[1].Y:F1} nose={NoseTip.X:F1},{NoseTip.Y:F1} snow={_snow:F2} has={(HasNose ? 1 : 0)} page={_sqk}/{YardSnowmanArt.YawPage(_yaw)}/{YardSnowmanArt.PitchPage(_pitch)}");
    }

    public override void Collect(DepthQueue q)
    {
        if (_art != null && _force != "nodraw") q.Add(_foot.Y, this, 0);       // "nodraw" is a cost-bisect aid: thinks but is not drawn
    }

    // ==================================================================
    //  SENSES: what draws his eye
    // ==================================================================

    private void Senses()
    {
        _near = S.Nearness(_foot.Y);
        if (Board.Time - _born < WakeLen) return;          // he is waking up: eyes shut, then a stretch; nothing draws his eye until he is up
        // SANTA comes through the SAME DOOR as everything else (the review: reading Board.Live for him was a second, unrationed
        // channel). Attention.Check rations him like any notice: a reaction to him is the FIRST sighting of a pass. That one
        // reaction opens the pass: ONE Watch (about seven seconds), or the hat tip, never both. While it lasts his head FOLLOWS
        // the sleigh: the target is the sleigh's own place (a scenery fact, ChristmasScenery.SantaSleigh), not the board.
        if (_mind.Doing is Doing.Watch or Doing.HatTip && _santaPass && S.SantaOnScreen) { _watchPt = S.SantaSleigh; _quietFor = 0; }
        else if (_mind.Doing is not (Doing.Watch or Doing.HatTip)) _santaPass = false;

        PointF me = new(_headX, _headY);
        Reaction r = _att.Check(Board, me, this, out Notice cause);
        if (r == Reaction.None) return;
        if (cause.Who is SantaRig)
        {
            // the first sighting of a pass (Check will not hand him over again for a long while)
            if (_mind.Doing is Doing.FixHat or Doing.Shake or Doing.Jump or Doing.Doze or Doing.Watch or Doing.HatTip) return;
            _passes++; _sinceTip++;
            _wantTip = _sinceTip >= 3 || (_sinceTip == 2 && Rng.NextDouble() < 0.6);
            MindLog.Open()?.WriteLine($"{Board.Time:F3} Snowman 0 ROLL santa pass {_passes} tip={_wantTip}");
            _santaPass = true; _watchPt = cause.At; _mood = Mood.Awe; _quietFor = 0;
            if (_wantTip) { _wantTip = false; _sinceTip = 0; Begin(Doing.HatTip, 3.9f); } else Begin(Doing.Watch, 7f);
            return;
        }
        _quietFor = 0;
        MindLog.Open()?.WriteLine($"{Board.Time:F3} Snowman 0 REACT {r} to {cause.Kind} from {(cause.Who?.GetType().Name ?? "scene")}");
        Doing d = _mind.Doing;
        bool sleeping = d == Doing.Doze;
        bool locked = d is Doing.HatTip or Doing.FixHat or Doing.Shake or Doing.Jump;
        if (locked) return;
        if (sleeping && cause.Kind != Sense.Loud && cause.Kind != Sense.Threat) return;   // a mere movement does not wake him
        _watchPt = cause.At;
        switch (cause.Kind)
        {
            case Sense.Threat:
                _mood = Mood.Suspicious; Begin(Doing.Watch, 3.2f); break;                 // narrow eyes at the fox: he cannot run, so he glares
            case Sense.Loud:
                if (r >= Reaction.Freeze && _jumpCool <= 0) { Begin(Doing.Jump, 1.9f); }
                else { _mood = Mood.Alert; Begin(Doing.Watch, 3.2f); }
                break;
            default:
                if (r == Reaction.Freeze) { _mood = Mood.Alert; Begin(Doing.Watch, 3.4f); }
                else { _mood = Mood.Curious; Begin(Doing.Watch, 3.0f); }
                break;
        }
    }

    // ==================================================================
    //  BUSINESS: what he does when nothing happens
    // ==================================================================

    private void Decide()
    {
        if (!_mind.Over) return;
        // Pieces of business come with a rest between them (Idle), so there is no metronome.
        if (_mind.Doing != Doing.Idle)
        {
            Begin(Doing.Idle, Organic.Jitter(Rng, 12f) + 3f);                 // a long quiet between pieces of business (he is alive in it: blinks, drifting eyes, the scarf)
            return;
        }
        bool noLull = !Lull && _lookLeft <= 0;
        if (!HasNose && noLull && Rng.NextDouble() < 0.75) { Begin(Doing.Search, 8.5f); return; }
        if (MathF.Abs(_hatSkew) > 0.10f && noLull) { Begin(Doing.FixHat, 2.6f); return; }
        float snowW = _snow > 0.55f ? (_snow - 0.45f) * 18f : 0f;
        if (_snow > 0.97f) snowW = 99f;
        float[] w = _weights;                                        // one array kept, refilled each roll
        w[0] = Lull ? 2.0f : 3.0f;                                   // Scan
        w[1] = noLull ? 2.0f : 0f;                                   // Shiver
        w[2] = noLull ? 1.3f : 0f;                                   // Snowflake
        w[3] = noLull ? 1.0f + _quietFor / 70f : 0.4f;               // Yawn
        w[4] = noLull && _dozeCool <= 0 && _quietFor > 25f ? 0.6f + _quietFor / 60f : 0f;   // Doze
        w[5] = noLull && _waveCool <= 0 ? 1.4f : 0f;                 // WaveCabin
        w[6] = noLull ? snowW : 0f;                                  // Shake
        w[7] = Lull ? 3.0f : 1.4f;                                   // Idle again
        int pick = Dice.Pick(Rng, w);
        MindLog.Open()?.WriteLine($"{Board.Time:F3} Snowman 0 ROLL business {pick} snow={_snow:F2} quiet={_quietFor:F0}");
        switch (pick)
        {
            case 0: Begin(Doing.Scan, 4.4f); break;
            case 1: Begin(Doing.Shiver, 3.6f); break;
            case 2: Begin(Doing.Snowflake, 4.6f); break;
            case 3: Begin(Doing.Yawn, 3.4f); break;
            case 4: Begin(Doing.Doze, 11.4f); break;
            case 5: Begin(Doing.WaveCabin, 5.8f); break;
            case 6: Begin(Doing.Shake, 1.9f); break;
            default: Begin(Doing.Idle, Organic.Jitter(Rng, 9f) + 3f); break;
        }
    }

    private void Begin(Doing d, float seconds)
    {
        _mind.Begin(d, seconds);
        _doT = 0;
        _hatHold = false; _hatLift = 0; _emitAcc = 0;
        switch (d)
        {
            case Doing.Scan: _scanDir = Rng.Next(2) == 0 ? -1 : 1; break;
            case Doing.HatTip:
            case Doing.FixHat:
                _hatTipMode = d == Doing.HatTip;
                _hatSide = Rng.Next(2) == 0 ? -1 : 1;
                break;
            case Doing.Search: _armSide = Rng.Next(2) == 0 ? -1 : 1; break;
            case Doing.WaveCabin: _waveCool = 45f; break;
            case Doing.Doze: _dozeCool = 90f; break;
            case Doing.Jump: _jumpCool = 10f; _jumpLanded = false; _jumpHatKicked = false; break;
            case Doing.Shake:
                _snowAtShake = _snow; _hatVy = -_z.T * 0.5f; _hatW = Rng.Next(2) == 0 ? 6f : -6f; break;
            case Doing.Snowflake:
                _flakeStart = new PointF(_headX + _z.T * 0.10f, _headY - _z.T * 0.80f); _flakeA = 0; break;
        }
        MindLog.Open()?.WriteLine($"{Board.Time:F3} Snowman 0 DOING {d} for {seconds:F1}");
    }

    /// <summary>Test aid: PECKWORKS_SNOWMAN=&lt;doing&gt; forces that piece of business at 6 seconds of the scene's clock.</summary>
    private void ForceTests()
    {
        if (_force == null) return;
        if (_noseTest)
        {
            if (!_forced && Board.Time >= 6.0) { _forced = true; TakeNose(); }
            if (_forced && !_noseGiven && Board.Time >= 22.0) { _noseGiven = true; GiveNose(); }
            return;
        }
        if (_forced || Board.Time < _forceAt) return;
        _forced = true;
        _watchPt = new PointF(S.Width * 0.55f, S.Height * 0.18f);
        switch (_force)
        {
            case "scan": Begin(Doing.Scan, 4.4f); break;
            case "shiver": Begin(Doing.Shiver, 3.6f); break;
            case "snowflake": Begin(Doing.Snowflake, 4.6f); break;
            case "yawn": Begin(Doing.Yawn, 3.4f); break;
            case "doze": Begin(Doing.Doze, 11.4f); break;
            case "wave": Begin(Doing.WaveCabin, 5.8f); break;
            case "shake": _snow = 0.9f; Begin(Doing.Shake, 1.9f); break;
            case "jump": Begin(Doing.Jump, 1.9f); break;
            case "hattip": _mood = Mood.Awe; Begin(Doing.HatTip, 3.9f); break;
            case "fixhat": _hatSkew = 0.4f; Begin(Doing.FixHat, 2.6f); break;
            case "watch": _mood = Mood.Curious; Begin(Doing.Watch, 3f); break;
            case "suspicious": _mood = Mood.Suspicious; Begin(Doing.Watch, 3f); break;
            case "search": TakeNose(); break;
            case "surprise": React(0); break;
            case "delight": React(1); break;
            case "cross": React(2); break;
        }
    }

    // ==================================================================
    //  GOALS
    // ==================================================================

    private static float Seg(float t, float a, float b) => Organic.Smooth((t - a) / (b - a));
    private static float Bump(float t, float c, float w) => t < c || t > c + w ? 0f : MathF.Sin(MathF.PI * (t - c) / w);

    /// <summary>A hand's resting place, from its shoulder: out and a little up, the classic snowman's arm (or hanging if he is sad).</summary>
    private PointF RestHand(int i) => HandUnit(i, 0.74f, -0.50f);

    /// <summary>A hand place in units of the arm's length: "out" is positive for both arms (it is mirrored for the left), "down" is positive.</summary>
    private PointF HandUnit(int i, float outward, float down) => new((i == 0 ? -outward : outward) * _z.ArmLen, down * _z.ArmLen);

    private void Arms(float outL, float downL, float outR, float downR, float spread = 0.45f)
    {
        _g.HandL = HandUnit(0, outL, downL); _g.HandR = HandUnit(1, outR, downR);
        _g.SpreadL = _g.SpreadR = spread;
    }

    /// <summary>Which way the head and eyes go to look at a place: the turn and nod of the head, and how far the glints slide.</summary>
    private void AimAt(PointF p, out float yaw, out float pitch, out float gx, out float gy)
    {
        float dx = p.X - _headX, dy = p.Y - _headY, u = S.U;
        yaw = Math.Clamp(dx / (0.30f * u), -1f, 1f) * YardSnowmanArt.YawMax * 0.95f;
        pitch = Math.Clamp(-dy / (0.25f * u) * 0.55f, -0.30f, 0.60f);
        gx = Math.Clamp(dx / (0.12f * u), -1f, 1f);
        gy = Math.Clamp(dy / (0.12f * u), -1f, 1f);
    }

    /// <summary>The default for a standing snowman: a gentle smile, a head that is never quite still, arms out.</summary>
    private void BaseGoal()
    {
        double t = Board.Time;
        bool sad = !HasNose;
        _g = default;
        _g.YawRate = 2.4f; _g.PitchRate = 8f;
        _g.Yaw = 0.20f * Organic.Drift(t * 0.55, _seed);
        _g.Pitch = 0.05f * Organic.Drift(t * 0.45, _seed + 3f) + (sad ? -0.16f : 0f);
        _g.Open = sad ? 0.82f : 1f;
        _g.GazeX = 0.55f * Organic.Drift(t * 0.8, _seed + 5f);
        _g.GazeY = 0.30f * Organic.Drift(t * 0.7, _seed + 8f);
        _g.MWidth = 0.50f; _g.MCurve = sad ? -0.18f : 0.22f; _g.MRingR = 0.12f; _g.MRingH = 1f; _g.MLat = -0.40f;
        _g.Blush = 0.30f;
        _g.Sink = sad ? 0.10f : 0f;
        if (sad) Arms(0.30f, 0.86f, 0.30f, 0.86f, 0.25f); else Arms(0.74f, -0.50f, 0.74f, -0.50f);
        // A hand never hangs dead still: a slow sway.
        // HIS ENTRANCE: he is there from the first frame the residents start, so he WAKES: eyes shut for a second, they open,
        // then a small stretch (both arms up a little, head tipped back) and down again. Each part is an eased move.
        double age = t - _born;
        if (age < WakeLen)
        {
            float a = (float)age;
            _g.Open = 0.04f + (_g.Open - 0.04f) * Seg(a, 1.0f, 1.7f);
            float st = Seg(a, 1.5f, 2.3f) * (1f - Seg(a, 2.9f, 3.7f));
            _g.Pitch += 0.20f * st; _g.Squash = 0.12f * st; _g.Sink = -0.04f * st;
            Arms(0.74f + (0.36f - 0.74f) * st, -0.50f + (-0.96f + 0.50f) * st, 0.74f + (0.36f - 0.74f) * st, -0.50f + (-0.96f + 0.50f) * st, 0.6f);
        }
        if (_lookLeft > 0) { AimAt(_lookPt, out float yw, out float pt, out float gx, out float gy); _g.Yaw = yw; _g.Pitch = pt; _g.GazeX = gx; _g.GazeY = gy; }
    }

    /// <summary>Each doing says what the face and body should be right now, from the doing's own clock.</summary>
    private void Act(float dt)
    {
        float t = (float)_doT, T = _z.T, R = _z.R2;
        double now = Board.Time;
        switch (_mind.Doing)
        {
            case Doing.Idle:
                break;

            case Doing.Watch:
            {
                AimAt(_watchPt, out float yw, out float pt, out float gx, out float gy);
                _g.Yaw = yw; _g.Pitch = pt; _g.GazeX = gx; _g.GazeY = gy;
                float lead = Seg(t, 0f, 0.25f);                      // the eyes get there first, the head follows
                switch (_mood)
                {
                    case Mood.Curious:
                        _g.Open = 1.1f; _g.Brow = 0.25f * lead; break;
                    case Mood.Alert:
                        _g.Open = 1.28f; _g.Brow = 0.8f * lead; _g.MRing = 0.55f * lead; _g.MRingR = 0.10f; _g.MCurve = 0f; _g.Squash = 0.12f * lead;
                        Arms(0.55f, -0.30f, 0.55f, -0.30f, 0.6f); break;
                    case Mood.Suspicious:
                        _g.Narrow = 1f; _g.Open = 0.82f; _g.MCurve = -0.02f; _g.MWidth = 0.40f; _g.BrowAng = -1f * lead; _g.Brow = -0.1f * lead; _g.Blush = 0.1f;
                        _g.Squash = -0.06f * lead; Arms(0.22f, 0.60f, 0.22f, 0.60f, 0.35f); break;
                    case Mood.Awe:
                        _g.Open = 1.2f; _g.MCurve = 0.30f; _g.MWidth = 0.62f; _g.Brow = 0.5f * lead; _g.Blush = 0.9f;
                    {
                        // Arms thrown up in wonder for the first few seconds, then they settle (a long pass is a long look).
                        float wow = 1f - Seg(t, 2.5f, 4.5f);
                        _g.Brow = 0.5f * lead * wow; _g.Open = 1.0f + 0.2f * wow;
                        Arms(0.74f, -0.50f - 0.24f * wow, 0.74f, -0.50f - 0.24f * wow, 0.6f); _g.OscL = _g.OscR = 0.05f * wow; _g.OscHz = 1.6f; break;
                    }
                }
                break;
            }

            case Doing.Scan:
            {
                float d = _scanDir, k = 0.60f;
                float turn = d * k * (Seg(t, 0.35f, 0.85f) - Seg(t, 1.7f, 2.15f)) - d * k * Seg(t, 2.35f, 2.85f) + d * k * Seg(t, 3.7f, 4.2f);
                _g.Yaw = turn; _g.YawRate = 2.0f;
                _g.GazeX = d * (0.95f * Seg(t, 0f, 0.3f) - 0.95f * Seg(t, 1.9f, 2.1f)) - d * 0.95f * Seg(t, 2.1f, 2.35f) + d * 0.95f * Seg(t, 3.7f, 3.9f);
                _g.GazeY = 0.1f;
                _g.Pitch = 0.04f + 0.04f * MathF.Sin(t * 1.3f);
                break;
            }

            case Doing.Shiver:
            {
                float ramp = Seg(t, 0f, 0.4f) * (1f - Seg(t, 3.0f, 3.5f));
                Arms(-0.60f, 0.34f, -0.60f, 0.34f, 0.3f);            // a hug: each hand to the far side of the chest
                _g.HandL = new PointF(0.60f * _z.ArmLen, 0.34f * _z.ArmLen); _g.HandR = new PointF(-0.60f * _z.ArmLen, 0.34f * _z.ArmLen);
                ShiverGoal(ramp, t);
                break;
            }

            case Doing.Snowflake:
                FlakeGoal(t, dt);
                break;

            case Doing.Yawn:
            {
                float inh = Seg(t, 0f, 0.7f) * (1f - Seg(t, 2.1f, 2.9f));
                _g.Pitch += 0.26f * inh; _g.Open = 1f - 0.85f * inh;
                _g.MRing = inh; _g.MRingR = 0.12f + 0.20f * inh; _g.MRingH = 1f + 0.25f * inh; _g.MCurve = 0.1f * (1 - inh);
                _g.Squash = 0.30f * inh; _g.Brow = 0.4f * inh; _g.Sink = -0.05f * inh;
                float arm = Seg(t, 0f, 0.8f) * (1f - Seg(t, 2.0f, 2.8f));
                Arms(0.74f + (0.36f - 0.74f) * arm, -0.50f + (-0.96f + 0.50f) * arm, 0.74f + (0.36f - 0.74f) * arm, -0.50f + (-0.96f + 0.50f) * arm, 0.65f);
                _g.OscL = _g.OscR = 0.025f * arm; _g.OscHz = 3f;
                break;
            }

            case Doing.Doze:
                DozeGoal(t, dt);
                break;

            case Doing.WaveCabin:
            {
                PointF cab = new(S.CabinDoor.X + S.CabinDoor.Width * 0.5f, S.CabinDoor.Y + S.CabinDoor.Height * 0.4f);
                AimAt(cab, out float yw, out float pt, out float gx, out float gy);
                _g.Yaw = yw; _g.Pitch = pt * 0.5f; _g.GazeX = gx; _g.GazeY = gy;
                int arm = cab.X < _foot.X ? 0 : 1;
                float up = Seg(t, 0.7f, 1.3f) * (1f - Seg(t, 4.6f, 5.2f));
                PointF raised = HandUnit(arm, 0.50f, -0.92f), rest = arm == 0 ? _g.HandL : _g.HandR;
                PointF h = new(rest.X + (raised.X - rest.X) * up, rest.Y + (raised.Y - rest.Y) * up);
                if (arm == 0) { _g.HandL = h; _g.OscL = 0.20f * Seg(t, 1.2f, 1.6f) * (1f - Seg(t, 4.3f, 4.8f)); _g.SpreadL = 0.75f; }
                else { _g.HandR = h; _g.OscR = 0.20f * Seg(t, 1.2f, 1.6f) * (1f - Seg(t, 4.3f, 4.8f)); _g.SpreadR = 0.75f; }
                _g.OscHz = 3.6f; _g.OscX = true;
                float glad = Seg(t, 0.9f, 1.4f) * (1f - Seg(t, 4.6f, 5.4f));
                _g.MCurve = 0.22f + 0.12f * glad; _g.MWidth = 0.5f + 0.2f * glad; _g.Blush = 0.3f + 0.6f * glad;
                _g.Happy = t > 1.6f && t < 4.2f && (t % 1.3f) < 1.0f;      // happy eyes, with a blink of the other kind now and then
                _g.Brow = 0.3f * glad;
                break;
            }

            case Doing.Shake:
                ShakeGoal(t, dt);
                break;

            case Doing.Jump:
                JumpGoal(t, dt);
                break;

            case Doing.HatTip:
                HatTipGoal(t, dt);
                break;

            case Doing.FixHat:
                FixHatGoal(t, dt);
                break;

            case Doing.Search:
                SearchGoal(t);
                break;
        }
    }

    // ---- the doings that need more than a few lines ----

    private void ShiverGoal(float ramp, float t)
    {
        float T = _z.T;
        float tr = MathF.Sin(t * MathF.Tau * 9f);
        _g.Lean = tr * 0.009f * T * ramp;
        _g.Tremble = MathF.Sin(t * MathF.Tau * 11f + 1f) * 0.006f * T * ramp;
        _g.MWob = ramp; _g.MCurve = 0.05f * (1 - ramp) - 0.04f * ramp; _g.MWidth = 0.46f;
        _g.Open = 1f - 0.40f * ramp;
        _g.Pitch = -0.12f * ramp; _g.Sink = 0.09f * ramp; _g.Squash = -0.10f * ramp;
        _g.Brow = 0.2f * ramp; _g.BrowAng = 0.7f * ramp;
        _g.OscL = _g.OscR = 0.012f * ramp; _g.OscHz = 9f;
    }

    /// <summary>
    /// A snowflake comes down out of the dark, wobbling, and lands on the tip of the carrot. His eyes follow it down and
    /// cross as it nears; he holds still, going cross-eyed at it, then it melts and he gives a little shake of the head.
    /// </summary>
    private void FlakeGoal(float t, float dt)
    {
        float T = _z.T;
        const float land = 1.9f;
        float k = Math.Clamp(t / land, 0f, 1f);
        PointF nose = NoseTip;
        // The path: straight down toward the nose with a sway that dies as it arrives.
        _flakeX = _flakeStart.X + (nose.X - _flakeStart.X) * k + 0.05f * T * MathF.Sin(k * 9f) * (1 - k);
        _flakeY = _flakeStart.Y + (nose.Y - _flakeStart.Y) * MathF.Pow(k, 1.15f);
        _flakeA = Seg(t, 0f, 0.25f) * (1f - Seg(t, 3.0f, 3.7f));
        float dx = _flakeX - _headX, dy = _flakeY - _headY;
        _g.GazeX = Math.Clamp(dx / (0.25f * T), -1f, 1f);
        _g.GazeY = Math.Clamp(dy / (0.25f * T), -1f, 1f);
        float near = Seg(t, 0.9f, land);
        float hold = 1f - Seg(t, 3.0f, 3.6f);
        _g.Cross = near * hold;
        _g.Pitch = -0.26f * Seg(t, 1.0f, land) * hold + 0.15f * (1f - Seg(t, 0f, 0.8f));      // first a glance up, then down at the nose
        _g.Open = 1.12f;
        _g.MRing = 0.7f * near * hold; _g.MRingR = 0.07f; _g.MCurve = 0.1f;
        if (t > 3.3f)                                                                    // the shake of the head, and a grin
        {
            float s = (t - 3.3f) / 1.1f;
            _g.Yaw = 0.30f * MathF.Sin(s * 22f) * (1 - Seg(s, 0.2f, 1f)); _g.YawDirect = false; _g.YawRate = 7f;
            _g.MCurve = 0.32f; _g.MWidth = 0.66f; _g.Blush = 0.8f;
        }
    }

    /// <summary>Nodding off: the lids droop, the head sinks, the hat slides, the little z's float up, a start awake.</summary>
    private void DozeGoal(float t, float dt)
    {
        float T = _z.T, R = _z.R2;
        float droop = Seg(t, 0.2f, 2.0f) * 0.55f + Seg(t, 2.0f, 3.0f) * 0.35f;
        float wake = Seg(t, 9.2f, 9.45f), calm = 1f - Seg(t, 10.0f, 10.8f);
        float asleep = Seg(t, 0.6f, 3.0f) * (1f - wake);
        _g.Open = (1f - droop) * (1f - wake) + 1.28f * wake * calm + (1f - wake) * 0f;
        if (wake > 0f && calm < 1f) _g.Open = MathF.Max(_g.Open, 1.0f * (1 - calm) + 1.28f * calm * wake);
        _g.Pitch = -0.04f - 0.42f * asleep - 0.10f * Bump(t, 5.4f, 0.55f) - 0.12f * Bump(t, 7.9f, 0.55f) + 0.20f * wake * calm;
        _g.PitchRate = wake > 0f ? 22f : 5f;
        _g.Sink = 0.28f * asleep;
        _g.Squash = -0.06f * asleep + 0.35f * Bump(t, 9.2f, 0.5f);
        _g.MCurve = 0f; _g.MRing = 0.8f * asleep; _g.MRingR = 0.06f + 0.035f * MathF.Sin(t * MathF.Tau * 0.45f); _g.MWidth = 0.4f;
        _g.Blush = 0.5f + 0.4f * wake * calm;
        if (wake > 0f) { _g.MRing = calm; _g.MRingR = 0.20f; _g.Brow = 1f * calm; }
        Arms(0.30f, 0.86f, 0.30f, 0.86f, 0.25f);                                        // arms hang limp
        // The hat slides forward in two slips, and is knocked askew when he starts awake.
        if (t < 9.2f) { _hatSkew = 0.14f * Seg(t, 1.0f, 3.0f) + 0.16f * Seg(t, 5.4f, 5.9f); _g.HatDy = 0.34f * R * Seg(t, 5.3f, 5.8f); }
        else { _g.HatDy = 0f; }
        // The z's float up from t = 3.2, one every 1.7 seconds.
        _emitAcc += dt;
        if (t > 3.2f && t < 9.0f && _emitAcc >= 1.7f)
        {
            _emitAcc = 0;
            Emit(2, _headX + R * 0.95f, _headY - R * 0.55f, T * 0.05f, -T * 0.10f, 2.8f, 0);
        }
        if (t >= 9.2f && !_jumpHatKicked) { _jumpHatKicked = true; _hatVy = -T * 0.7f; _hatW = 7f; }
        if (t < 9.2f) _jumpHatKicked = false;
    }

    /// <summary>
    /// A jump: crouch, spring up (stretching), hang, come down (squashing), settle with a wobble. The hat pops off the
    /// head at take-off, comes down at the landing and lands askew. Arms fly up. The body is set outright: a hop is a
    /// parabola under gravity, and an eased glide would float.
    /// </summary>
    private void JumpGoal(float t, float dt)
    {
        float T = _z.T, R = _z.R2;
        const float launch = 0.20f, air = 0.66f;
        float land = launch + air;
        _g.Direct = true;
        float lift = 0f, sq;
        if (t < 0.14f) sq = -0.9f * Seg(t, 0f, 0.14f);
        else if (t < launch) sq = -0.9f + 1.7f * Seg(t, 0.14f, launch);
        else if (t < land)
        {
            float k = (t - launch) / air;
            lift = 0.17f * T * 4f * k * (1f - k);
            sq = 0.8f * (1f - Seg(k, 0.0f, 0.85f)) * (1f - k * 0.2f);
        }
        else
        {
            float s = t - land;
            sq = -1.0f * MathF.Exp(-s * 6.5f) * MathF.Cos(s * 12f);            // a landing squash that rings out
        }
        _g.Lift = lift; _g.Squash = sq;
        _g.Open = 1.28f; _g.Brow = 1f * (1f - Seg(t, 1.3f, 1.9f)); _g.MRing = 1f * (1f - Seg(t, 1.2f, 1.6f)); _g.MRingR = 0.20f; _g.MCurve = 0.1f;
        _g.Cross = 0f; _g.GazeX = 0f; _g.GazeY = -0.2f;
        float up = Seg(t, 0.10f, 0.35f) * (1f - Seg(t, 1.2f, 1.8f));
        Arms(0.56f * up + 0.74f * (1 - up), -0.90f * up - 0.50f * (1 - up), 0.56f * up + 0.74f * (1 - up), -0.90f * up - 0.50f * (1 - up), 0.8f);
        _g.OscL = _g.OscR = 0.05f * up; _g.OscHz = 7f;
        _g.Pitch = 0.10f * Bump(t, launch, 0.7f);
        // The hat: kicked off at take-off, thumped at landing, left askew.
        if (t >= launch && !_jumpHatKicked) { _jumpHatKicked = true; _hatVy = -T * 1.25f; _hatW = Rng.Next(2) == 0 ? 7f : -7f; }
        if (t >= land && !_jumpLanded)
        {
            _jumpLanded = true;
            _hatVy = T * 1.0f; _hatW = _hatW > 0 ? 9f : -9f;
            _hatSkew = (Rng.Next(2) == 0 ? -1f : 1f) * (0.30f + 0.18f * Rng.NextSingle());
            for (int i = 0; i < 14; i++)                                            // the puff of snow where he lands
                Emit(0, _foot.X + (Rng.NextSingle() - 0.5f) * _z.R0 * 2.2f, _foot.Y - 1f, (Rng.NextSingle() - 0.5f) * T * 1.1f, -T * (0.15f + 0.5f * Rng.NextSingle()), 0.6f + 0.3f * Rng.NextSingle(), Rng.Next(3));
        }
    }

    /// <summary>
    /// He looks up at Santa, a hand goes to the brim, the hat comes off and up toward the sky (a few sparkles fall off it),
    /// and it goes back on. The hat is HELD for the middle of this: it goes exactly where the hand goes.
    /// </summary>
    private void HatTipGoal(float t, float dt)
    {
        float T = _z.T, R = _z.R2;
        AimAt(_watchPt, out float yw, out float pt, out float gx, out float gy);
        _g.Yaw = yw; _g.Pitch = pt; _g.GazeX = gx; _g.GazeY = gy;
        _g.Open = 1.2f; _g.MCurve = 0.30f; _g.MWidth = 0.62f; _g.Blush = 0.9f; _g.Brow = 0.5f;
        const float grab = 0.55f, up = 1.05f, holdEnd = 2.55f, down = 3.05f, release = 3.1f;
        int hi = _hatSide < 0 ? 0 : 1, other = 1 - hi;
        if (hi == 0) { _g.HandR = HandUnit(1, 0.74f, -0.74f); } else { _g.HandL = HandUnit(0, 0.74f, -0.74f); }   // the free arm waves a little
        if (t < grab) HandToHat(hi);
        else if (t < release)
        {
            _hatHold = true;
            _hatLift = Seg(t, grab, up) * (1f - Seg(t, holdEnd, down));
            _hatHeldAng = _hatSide * 0.55f * _hatLift + 0.10f * _hatLift * MathF.Sin(t * 9f);
            _g.SpreadL = _g.SpreadR = 0.15f;
            if (_hatLift > 0.55f)                                                    // sparkles fall from the lifted hat
            {
                _emitAcc += dt * 16f;
                while (_emitAcc >= 1f)
                {
                    _emitAcc -= 1f;
                    PointF top = HatTop;
                    Emit(1, top.X + (Rng.NextSingle() - 0.5f) * R * 1.6f, top.Y + R * 0.8f * Rng.NextSingle(), (Rng.NextSingle() - 0.5f) * T * 0.12f, T * (0.02f + 0.10f * Rng.NextSingle()), 1.3f + 0.5f * Rng.NextSingle(), Rng.Next(2));
                }
            }
        }
    }

    /// <summary>A hand goes up and sets the crooked hat straight, with a little jiggle, and lets go.</summary>
    private void FixHatGoal(float t, float dt)
    {
        float T = _z.T, R = _z.R2;
        _g.Pitch = 0.05f; _g.Open = 0.9f;
        const float grab = 0.55f, release = 1.9f;
        int hi = _hatSide < 0 ? 0 : 1;
        if (t < grab) { HandToHat(hi); _fixFrom = _hatA; }
        else if (t < release)
        {
            _hatHold = true;
            _hatLift = Seg(t, grab, 0.9f) * (1f - Seg(t, 1.5f, release));
            float straighten = Seg(t, 0.8f, 1.5f);
            _hatHeldAng = _fixFrom * (1f - straighten) + 0.06f * MathF.Sin(t * 14f) * (1f - straighten) * (1f - 0.5f * straighten);
            _g.SpreadL = _g.SpreadR = 0.15f;
        }
        else if (t >= release && MathF.Abs(_hatSkew) > 0f)
        {
            _hatSkew = 0f; _hatA = 0f; _hatW = 0f;                                       // it is straight now, and stays so
            _g.MCurve = 0.3f;
        }
        if (t >= release) { _g.MCurve = 0.32f; _g.Blush = 0.7f; }
    }

    private float _fixFrom;

    private void HandToHat(int arm)
    {
        PointF edge = HatEdge(arm == 0 ? -1 : 1, _hatCx, _hatCy, _hatA);
        PointF rel = new(edge.X - _sh[arm].X, edge.Y - _sh[arm].Y);
        if (arm == 0) { _g.HandL = rel; _g.SpreadL = 0.3f; } else { _g.HandR = rel; _g.SpreadR = 0.3f; }
    }

    private PointF HatEdge(int side, float cx, float cy, float ang)
    {
        float vx = side * 0.95f * _z.R2;
        return new PointF(cx + vx * MathF.Cos(ang), cy + vx * MathF.Sin(ang));
    }

    /// <summary>The snow comes off: a quick shake of the whole head and body, the hat bouncing, powder flying off the hat, the arms and the scarf.</summary>
    private void ShakeGoal(float t, float dt)
    {
        float T = _z.T, R = _z.R2;
        float ramp = 1f - Seg(t, 1.0f, 1.8f);
        _g.Yaw = 0.52f * MathF.Sin(t * MathF.Tau * 5.5f) * ramp; _g.YawDirect = true;
        _g.Lean = MathF.Sin(t * MathF.Tau * 9f) * 0.010f * T * ramp;
        _g.Squash = 0.10f * MathF.Sin(t * MathF.Tau * 9f + 1f) * ramp;
        _g.Open = 0.12f * ramp + (1f - ramp); _g.MWob = ramp; _g.MWidth = 0.46f; _g.MCurve = 0.05f; _g.Brow = 0.2f * ramp;
        _g.HatDx = 0.35f * R * MathF.Sin(t * MathF.Tau * 9f + 1f) * ramp;
        _g.OscL = _g.OscR = 0.14f * ramp; _g.OscHz = 8f; _g.OscX = false;
        Arms(0.62f, -0.10f, 0.62f, -0.10f, 0.6f);
        _snow = _snowAtShake * (1f - Seg(t, 0.25f, 1.25f));
        if (t > 0.1f && t < 1.3f)
        {
            _emitAcc += dt * 70f * (0.3f + _snowAtShake);
            while (_emitAcc >= 1f)
            {
                _emitAcc -= 1f;
                int where = Rng.Next(4);
                float px, py;
                if (where == 0) { px = _hatCx + (Rng.NextSingle() - 0.5f) * R * 1.8f; py = _hatCy - R * 0.6f * Rng.NextSingle(); }          // off the hat
                else if (where == 1) { px = _sh[0].X + (Rng.NextSingle() - 0.5f) * R; py = _sh[0].Y; }
                else if (where == 2) { px = _sh[1].X + (Rng.NextSingle() - 0.5f) * R; py = _sh[1].Y; }
                else { px = _headX + (Rng.NextSingle() - 0.5f) * R * 2f; py = _neckY - R * 0.3f; }                                        // off the scarf
                Emit(0, px, py, (Rng.NextSingle() - 0.5f) * T * 0.9f, -T * (0.05f + 0.35f * Rng.NextSingle()), 0.7f + 0.4f * Rng.NextSingle(), Rng.Next(3));
            }
        }
    }

    /// <summary>No carrot: a hand feels for it (and pats the spot), the eyes widen, then he droops, looks about the ground and sighs.</summary>
    private void SearchGoal(float t)
    {
        float T = _z.T;
        int arm = _armSide < 0 ? 0 : 1;
        float reach = Seg(t, 0.2f, 0.7f) * (1f - Seg(t, 2.3f, 2.9f));
        YardSnowmanArt.FacePoint(0, -0.06f, _yaw, _pitch, _z.R2, out float sx, out float sy, out _);
        float tap = 0.022f * T * MathF.Abs(MathF.Sin(t * MathF.Tau * 2.6f)) * Seg(t, 0.8f, 1.1f) * (1f - Seg(t, 2.0f, 2.3f));
        PointF spot = new(_headX + sx + _armSide * 0.020f * T - _sh[arm].X, _headY + sy + 0.012f * T + tap - _sh[arm].Y);
        PointF rest = arm == 0 ? _g.HandL : _g.HandR;
        PointF h = new(rest.X + (spot.X - rest.X) * reach, rest.Y + (spot.Y - rest.Y) * reach);
        if (arm == 0) { _g.HandL = h; _g.SpreadL = 0.35f; } else { _g.HandR = h; _g.SpreadR = 0.35f; }
        _g.Open = 1.2f * (1f - Seg(t, 1.0f, 1.6f)) + 0.85f * Seg(t, 1.0f, 1.6f);
        _g.Pitch = -0.28f * Seg(t, 0f, 0.5f);
        _g.Cross = 0.55f * Bump(t, 0.5f, 2.0f);
        _g.Brow = 0.4f * Bump(t, 0f, 2.4f); _g.BrowAng = 0.9f * Seg(t, 1.8f, 2.6f);
        if (t > 2.6f && t < 6.2f)
        {
            _g.Yaw = 0.5f * MathF.Sin((t - 2.6f) * 1.15f);
            _g.GazeX = 0.9f * MathF.Sin((t - 2.6f) * 1.15f + 0.5f); _g.GazeY = 0.6f;
            _g.Pitch = -0.30f;
        }
        _g.Sink = 0.10f + 0.10f * Bump(t, 6.2f, 1.8f);
        _g.MCurve = -0.18f - 0.06f * Bump(t, 6.2f, 1.8f);
    }

    /// <summary>A story's react() overlays a face and a body on whatever he is doing, for 2 seconds.</summary>
    private void ReactGoal()
    {
        float t = _reactT, T = _z.T;
        float on = Seg(t, 0f, 0.12f) * (1f - Seg(t, 1.7f, 2.0f));
        switch (_reactKind)
        {
            case 0:
                _g.Open = 1.28f; _g.MRing = on; _g.MRingR = 0.20f; _g.Brow = on; _g.Squash = 0.2f * on;
                Arms(0.56f, -0.90f, 0.56f, -0.90f, 0.8f);
                _g.Direct = true; _g.Lift = 0.05f * T * Bump(t, 0f, 0.5f); _g.Squash = _g.Squash - 0.5f * Bump(t, 0.5f, 0.2f);
                break;
            case 1:
                _g.Happy = on > 0.5f; _g.MCurve = 0.34f; _g.MWidth = 0.70f; _g.Blush = 1f; _g.Brow = 0.4f * on;
                Arms(0.78f, -0.74f, 0.78f, -0.74f, 0.7f); _g.OscL = _g.OscR = 0.08f * on; _g.OscHz = 5.5f;
                _g.Direct = true; _g.Lift = 0.045f * T * MathF.Abs(MathF.Sin(t * MathF.PI * 2.2f)) * on; _g.Squash = -0.15f * (1 - MathF.Abs(MathF.Sin(t * MathF.PI * 2.2f))) * on;
                break;
            default:
                _g.Narrow = on; _g.Open = 0.85f; _g.MCurve = -0.22f; _g.MWidth = 0.42f; _g.BrowAng = -1f * on; _g.Brow = -0.1f * on; _g.Blush = 0.1f;
                Arms(0.20f, 0.62f, 0.20f, 0.62f, 0.3f); _g.Squash = -0.12f * on;
                _g.Yaw = 0.25f * MathF.Sin(t * 14f) * on * (1f - Seg(t, 0.8f, 1.4f)); _g.YawDirect = true; _g.Pitch = -0.10f * on;
                break;
        }
    }

    // ==================================================================
    //  EASING AND LAYOUT
    // ==================================================================

    private void Ease(float dt)
    {
        float K(float rate) => 1f - MathF.Exp(-rate * dt);
        // A "direct" turn (a head shake) is still smoothed a little (about 25 ms), so its first frame continues the idle head.
        if (_g.YawDirect) _yaw += (_g.Yaw - _yaw) * K(40f); else _yaw = Turn.Toward(_yaw, Math.Clamp(_g.Yaw, -YardSnowmanArt.YawMax, YardSnowmanArt.YawMax), _g.YawRate, dt);
        _pitch += (Math.Clamp(_g.Pitch, YardSnowmanArt.PitchMin, YardSnowmanArt.PitchMin + 6 * YardSnowmanArt.PitchStep) - _pitch) * K(_g.PitchRate);
        _open += (_g.Open - _open) * K(20f);
        _narrow += (_g.Narrow - _narrow) * K(14f);
        _happy = _g.Happy;
        _gazeX += (_g.GazeX - _gazeX) * K(26f); _gazeY += (_g.GazeY - _gazeY) * K(26f); _cross += (_g.Cross - _cross) * K(10f);
        _mCurve += (_g.MCurve - _mCurve) * K(12f); _mWidth += (_g.MWidth - _mWidth) * K(12f); _mRing += (_g.MRing - _mRing) * K(14f);
        _mRingR += (_g.MRingR - _mRingR) * K(12f); _mRingH += (_g.MRingH - _mRingH) * K(12f); _mWob += (_g.MWob - _mWob) * K(14f); _mLat += (_g.MLat - _mLat) * K(8f);
        _brow += (_g.Brow - _brow) * K(14f); _browAng += (_g.BrowAng - _browAng) * K(14f); _blush += (_g.Blush - _blush) * K(5f);
        _sink += (_g.Sink - _sink) * K(9f); _lean += (_g.Lean - _lean) * K(14f); _tremble = _g.Tremble;
        if (_g.Direct) { _squash = _g.Squash; _lift = _g.Lift; }
        else { _squash += (_g.Squash - _squash) * K(14f); _lift += (_g.Lift - _lift) * K(16f); }
        float ka = K(8f), ks = K(10f);
        // A hand never moves faster than three body heights a second (about 11 px a frame at 1080p): an arm is flung up over a
        // third of a second, not in one frame. The ease alone allowed 25 px steps at the jump.
        float cap = 3.0f * _z.T * dt;
        for (int i = 0; i < 2; i++)
        {
            PointF goal = i == 0 ? _g.HandL : _g.HandR;
            float sx = (goal.X - _hand[i].X) * ka, sy = (goal.Y - _hand[i].Y) * ka, sl = MathF.Sqrt(sx * sx + sy * sy);
            if (sl > cap) { sx *= cap / sl; sy *= cap / sl; }
            _hand[i] = new PointF(_hand[i].X + sx, _hand[i].Y + sy);
        }
        _spread[0] += (_g.SpreadL - _spread[0]) * ks; _spread[1] += (_g.SpreadR - _spread[1]) * ks;
    }

    private void Blink(float dt)
    {
        _blinkLeft -= dt; _nextBlink -= dt;
        if (_nextBlink <= 0f)
        {
            _blinkLeft = 0.15f;
            _nextBlink = Rng.NextDouble() < 0.2 ? 0.24f : Organic.Jitter(Rng, 4.2f);       // now and then a double blink
        }
    }

    /// <summary>Where every ball and shoulder is this frame, from the squash, lift, lean and sink.</summary>
    private void Layout()
    {
        SnowmanSize z = _z;
        float kf = Math.Clamp(_squash * 5f, -5f, 5f);
        int k = (int)MathF.Floor(kf);
        _sqk = k + YardSnowmanArt.SquashMid; _sqf = kf - k;
        float sy = 1f + 0.045f * kf, sx = 1f / MathF.Sqrt(sy);       // the body's real size follows the squash continuously (it used to step with the page)
        float foot = _foot.Y - _lift;
        // The bottom ball's silhouette hangs a hair below the contact point (we look down on it a little).
        _c0y = foot + 0.08f * z.R0 - z.R0 * sy;
        float top0 = _c0y - z.R0 * sy;
        _c1y = top0 + z.Ov - z.R1 * sy;
        float top1 = _c1y - z.R1 * sy;
        _midX = _foot.X + _lean * 0.5f;
        _headX = _foot.X + _lean + _tremble;
        float headRest = top1 + z.Ov - z.R2;
        _headY = headRest + _sink * z.R2;
        _neckY = headRest + 0.95f * z.R2;
        float shy = _c1y - 0.16f * z.R1 * sy;
        _sh[0] = new PointF(_midX - 0.90f * z.R1 * sx, shy);
        _sh[1] = new PointF(_midX + 0.90f * z.R1 * sx, shy);
    }

    /// <summary>
    /// The two-bone arm: given the hand's place from the shoulder, find the elbow (a triangle with two equal sides, the
    /// bones, and the third side the shoulder-to-hand distance, by the law of cosines). Of the two places the elbow could
    /// be, take the one further out and down, where a real elbow goes.
    /// </summary>
    private void Reach(int i, PointF hand)
    {
        float L = _z.ArmLen, a = L * 0.5f;
        float hx = hand.X, hy = hand.Y;
        float dl = MathF.Max(1e-3f, MathF.Sqrt(hx * hx + hy * hy));
        float dc = Math.Clamp(dl, 0.2f * L, 0.995f * L);
        float ux = hx / dl, uy = hy / dl;
        float cosA = Math.Clamp(dc / (2f * a), -1f, 1f);           // equal bones: cos(angle at the shoulder) = d / 2a
        float A = MathF.Acos(cosA), base0 = MathF.Atan2(uy, ux), side = i == 0 ? -1f : 1f;
        float e1x = a * MathF.Cos(base0 + A), e1y = a * MathF.Sin(base0 + A), e2x = a * MathF.Cos(base0 - A), e2y = a * MathF.Sin(base0 - A);
        bool first = side * e1x + 0.5f * e1y >= side * e2x + 0.5f * e2y;
        float ex = first ? e1x : e2x, ey = first ? e1y : e2y;
        _elb[i] = new PointF(_sh[i].X + ex, _sh[i].Y + ey);
        // The wrist is the hand itself (only a hand reaching further than the arm is pulled back). It must NOT use the clamped
        // distance: a hand passing under its own shoulder (the hug) used to jump 2 x 0.2 L as its direction flipped.
        float wk = MathF.Min(1f, 0.995f * L / dl);
        _wr[i] = new PointF(_sh[i].X + hx * wk, _sh[i].Y + hy * wk);
    }

    private void ArmsAndHat(float dt)
    {
        float R = _z.R2, T = _z.T;
        // The hat's rest place, on the head (a head nodded back lets the hat slip down a little).
        float restX = _headX, restY = _headY - 0.80f * R + R * (1f - MathF.Cos(_pitch)) * 0.8f;

        // ---- the hat: a spring toward its target, unless a hand is holding it ----
        if (_hatHold)
        {
            float side = _hatSide;
            float lx = _hatTipMode ? side * 0.70f * R * _hatLift : 0f, ly = _hatTipMode ? -1.00f * R * _hatLift : -0.35f * R * _hatLift;
            _hatX = lx; _hatY = ly; _hatVx = 0; _hatVy = 0; _hatA = _hatHeldAng; _hatW = 0;
        }
        else
        {
            const int steps = 2;
            float h = dt / steps;
            for (int s = 0; s < steps; s++)
            {
                _hatVx += (-170f * (_hatX - _g.HatDx) - 11f * _hatVx) * h; _hatX += _hatVx * h;
                _hatVy += (-170f * (_hatY - _g.HatDy) - 11f * _hatVy) * h; _hatY += _hatVy * h;
                _hatW += (-110f * (_hatA - _hatSkew) - 7f * _hatW) * h; _hatA += _hatW * h;
            }
            _hatA = Math.Clamp(_hatA, -0.7f, 0.7f);
        }
        _hatCx = restX + _hatX; _hatCy = restY + _hatY;

        // ---- the arms: eased hand + any wave, then the elbow ----
        for (int i = 0; i < 2; i++)
        {
            PointF h = _hand[i];
            float sway = 0.02f * _z.ArmLen;
            h = new PointF(h.X + sway * Organic.Drift(Board.Time * 0.9, _seed + i * 1.7f), h.Y + sway * Organic.Drift(Board.Time * 0.7, _seed + 4f + i));
            float amp = (i == 0 ? _g.OscL : _g.OscR) * _z.ArmLen;
            if (amp > 0f)
            {
                float o = MathF.Sin((float)(Board.Time * _g.OscHz * MathF.Tau % MathF.Tau)) * amp;
                h = _g.OscX ? new PointF(h.X + o, h.Y + 0.25f * MathF.Abs(o) * -0.3f) : new PointF(h.X, h.Y + o);
            }
            if (_hatHold && i == (_hatSide < 0 ? 0 : 1))
            {
                PointF edge = HatEdge(_hatSide, _hatCx, _hatCy, _hatA);
                h = new PointF(edge.X - _sh[i].X, edge.Y - _sh[i].Y);
                _hand[i] = h;                                              // the eased hand is where the hat put it: no pop when it lets go
            }
            Reach(i, h);
        }
    }

    // ==================================================================
    //  PARTICLES
    // ==================================================================

    private void Emit(int kind, float x, float y, float vx, float vy, float life, int size)
    {
        for (int i = 0; i < Pool; i++)
        {
            if (_pLife[i] > 0f) continue;
            _pKind[i] = kind; _pX[i] = x; _pY[i] = y; _pVx[i] = vx; _pVy[i] = vy; _pAge[i] = 0f; _pLife[i] = life; _pPh[i] = size + Rng.NextSingle() * 6f;
            return;
        }
    }

    private void Particles(float dt)
    {
        float T = _z.T;
        for (int i = 0; i < Pool; i++)
        {
            if (_pLife[i] <= 0f) continue;
            _pAge[i] += dt;
            if (_pAge[i] >= _pLife[i]) { _pLife[i] = 0f; continue; }
            switch (_pKind[i])
            {
                case 0:
                    _pVy[i] += 2.6f * T * dt; _pVx[i] *= 1f - 0.8f * dt;
                    _pX[i] += _pVx[i] * dt; _pY[i] += _pVy[i] * dt;
                    if (_pVy[i] > 0 && _pY[i] > _foot.Y + 0.02f * T) _pLife[i] = 0f;      // it landed on the snow
                    break;
                case 1:
                    _pVy[i] = MathF.Min(_pVy[i] + 0.55f * T * dt, 0.28f * T); _pVx[i] *= 1f - 1.2f * dt;
                    _pX[i] += _pVx[i] * dt; _pY[i] += _pVy[i] * dt;
                    break;
                default:
                    _pX[i] += (_pVx[i] + 0.04f * T * MathF.Cos(_pAge[i] * 2.1f)) * dt; _pY[i] += _pVy[i] * dt;
                    break;
            }
        }
    }

    // ==================================================================
    //  DRAWING
    // ==================================================================

    private void Put(FrameBuffer fb, SnowPage p, float x, float y, float opacity = 1f, bool mirror = false) =>
        p.Img.DrawSmooth(fb, x - (mirror ? p.Img.Width - 1 - p.Ox : p.Ox), y - p.Oy, opacity * _vis, null, mirror, S.NearDepth, _near);   // at a fraction of a pixel: his sway and squash glide

    /// <summary>Two pages blended: "a" at full strength, then "b" laid over it at "f" (so f = 0 is a, f = 1 is b).</summary>
    private void PutMix(FrameBuffer fb, SnowPage a, SnowPage b, float f, float x, float y, float opacity = 1f, bool mirror = false)
    {
        Put(fb, a, x, y, opacity, mirror);
        if (f > 0.02f) Put(fb, b, x, y, opacity * f, mirror);
    }

    private void Line(FrameBuffer fb, PointF a, PointF b, Color c, float alpha, float thick) => fb.Line(a, b, c, alpha * _vis, thick, null, S.NearDepth, _near);

    public override void DrawOne(FrameBuffer fb, int index)
    {
        long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
        DrawInner(fb);
        double ms = (System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        _sumDraw += ms;
        if (ms > _worstDraw && ms > 0.5 && MindLog.Open() is { } log)
        {
            _worstDraw = ms;
            log.WriteLine($"{Board.Time:F3} Snowman 0 SLOW draw {ms:F2} ms frame {_frameNo} doing {_mind.Doing}");
        }
    }

    private void DrawInner(FrameBuffer fb)
    {
        YardSnowmanArt? art = _art;
        if (art == null) return;
        { float k = _born < 0 ? 0f : Math.Clamp((float)((Board.Time - _born) / 0.8), 0f, 1f); _vis = k * k * (3f - 2f * k); }
        SnowmanSize z = _z;
        float R = z.R2;

        // 1. The shadow on the snow: down and to the right (the moon is up and left), shrinking as he leaves the ground.
        float air = Math.Clamp(1f - _lift / (0.45f * z.T), 0.3f, 1f);
        Put(fb, art.Shadow, _foot.X + 0.55f * z.R0, _foot.Y + 0.02f * z.R0, air);
        // 2. The two lower balls.
        int sq1 = Math.Min(_sqk + 1, art.Bottom.Length - 1);
        PutMix(fb, art.Bottom[_sqk], art.Bottom[sq1], _sqf, _foot.X, _c0y);
        PutMix(fb, art.Mid[_sqk], art.Mid[sq1], _sqf, _midX, _c1y);
        // 3. The head.
        Put(fb, art.Head, _headX, _headY);
        int yp = YardSnowmanArt.YawPage(_yaw), pp = YardSnowmanArt.PitchPage(_pitch);
        // the nose is one of 25 x 7 pages: the yaw and pitch are blended between the pages either side
        float yawStep = 2 * YardSnowmanArt.YawMax / (YardSnowmanArt.YawPages - 1);
        float yf = Math.Clamp(_yaw / yawStep + YardSnowmanArt.YawPages / 2, 0f, YardSnowmanArt.YawPages - 1f), pf = Math.Clamp((_pitch - YardSnowmanArt.PitchMin) / YardSnowmanArt.PitchStep, 0f, YardSnowmanArt.PitchPages - 1f);
        int yl = Math.Min((int)yf, YardSnowmanArt.YawPages - 2), pl = Math.Min((int)pf, YardSnowmanArt.PitchPages - 2); float yfr = yf - yl, pfr = pf - pl;
        float yawQ = YardSnowmanArt.YawOf(yp);

        // 4. The face: cheeks, brows, eyes, glints, mouth, nose (the nose last: turned, it hides the far eye).
        for (int e = 0; e < 2; e++)
        {
            float lon = e == 0 ? -0.80f : 0.80f;
            YardSnowmanArt.FacePoint(lon, -0.10f, _yaw, _pitch, R, out float bx, out float by, out float bz);
            if (bz > 0.20f) Put(fb, art.Blush, _headX + bx, _headY + by, Math.Clamp((bz - 0.10f) * 1.6f, 0f, 1f) * Math.Clamp(0.35f + 0.65f * _blush, 0f, 1f));
        }
        float blink = _blinkLeft > 0f ? MathF.Sin(MathF.PI * (1f - _blinkLeft / 0.15f)) : 0f;
        float openEff = Math.Clamp(_open * (1f - 0.92f * blink), 0.05f, 1.3f);
        // THE LID: the two eye pages either side of how open the eye is, in proportion (the lid glides; it used to step through 11 pages)
        int level = 0;
        while (level < YardSnowmanArt.EyeLevels - 2 && openEff >= YardSnowmanArt.EyeOpen[level + 1]) level++;
        float lidF = Math.Clamp((openEff - YardSnowmanArt.EyeOpen[level]) / (YardSnowmanArt.EyeOpen[level + 1] - YardSnowmanArt.EyeOpen[level]), 0f, 1f);
        for (int e = 0; e < 2; e++)
        {
            float lon = e == 0 ? -0.46f : 0.46f;
            YardSnowmanArt.FacePoint(lon, 0.18f, _yaw, _pitch, R, out float ex, out float ey, out float ez);
            if (ez < 0.14f) continue;
            int sq = ez > 0.86f ? 0 : ez > 0.68f ? 1 : ez > 0.48f ? 2 : 3;
            float x = _headX + ex, y = _headY + ey;
            bool mirror = e == 1;
            float wk = YardSnowmanArt.EyeWidthK[sq], er = z.EyeR;
            if (_happy) Put(fb, art.EyeHappy[sq], x, y);
            else
            {
                bool cut = _narrow > 0.5f && level >= YardSnowmanArt.EyeCutFrom && level <= YardSnowmanArt.EyeCutTo;
                PutMix(fb, art.Eye[cut ? 1 : 0, level, sq], art.Eye[cut ? 1 : 0, level + 1, sq], lidF, x, y, 1f, mirror);
                if (openEff >= YardSnowmanArt.EyeOpen[2])
                {
                    float gxn = Math.Clamp(_gazeX + (e == 0 ? _cross : -_cross) * 0.85f, -1f, 1f), gyn = Math.Clamp(_gazeY + _cross * 0.55f, -1f, 1f);
                    float gx = x - 0.28f * er * wk + gxn * 0.50f * er * wk, gy = y - 0.30f * er + gyn * 0.42f * er * MathF.Min(1f, openEff);
                    Put(fb, art.Glint, gx, gy, Math.Clamp(ez * 1.2f, 0f, 1f) * Math.Clamp(openEff * 1.5f, 0f, 1f));
                }
            }
            // The twig brow over it.
            float bAmt = MathF.Abs(_brow) + MathF.Abs(_browAng);
            if (bAmt > 0.12f)
            {
                YardSnowmanArt.FacePoint(lon, 0.50f, _yaw, _pitch, R, out float brx, out float bry, out float brz);
                if (brz > 0.2f)
                {
                    float inner = e == 0 ? 1f : -1f;                                  // toward the nose
                    float half = 0.27f * R * wk * 0.5f + 0.13f * R;
                    float rise = _brow * 0.20f * R + -MathF.Abs(_browAng) * 0 ;
                    float tilt = _browAng * 0.20f * R;                                 // positive sad: the inner end up; negative cross: the inner end down
                    PointF a = new(_headX + brx - inner * half, _headY + bry - rise), b = new(_headX + brx + inner * half, _headY + bry - rise - tilt);
                    float al = Math.Clamp(bAmt / 0.4f, 0f, 1f) * Math.Clamp(brz * 1.6f, 0f, 1f);
                    Line(fb, a, b, Color.FromArgb(98, 68, 44), al, MathF.Max(1.3f, z.Thick * 0.8f));
                }
            }
        }
        // The mouth: nine coal dots on an arc that morphs to a ring.
        for (int i = 0; i < 9; i++)
        {
            float u = i / 8f * 2f - 1f;
            float lonA = u * _mWidth, latA = _mLat - _mCurve * (1f - u * u);
            float a = MathF.PI * 0.5f + i * MathF.Tau / 9f;
            float lonR = _mRingR * MathF.Cos(a) * 0.9f, latR = _mLat + _mRingR * MathF.Sin(a) * _mRingH;
            float lon = lonA + (lonR - lonA) * _mRing, lat = latA + (latR - latA) * _mRing;
            if (_mWob > 0.02f) lat += _mWob * 0.045f * ((i & 1) == 0 ? 1f : -1f) * MathF.Sin((float)(Board.Time * 13.0 * MathF.Tau % MathF.Tau));
            YardSnowmanArt.FacePoint(lon, lat, _yaw, _pitch, R, out float mx, out float my, out float mz);
            if (mz < 0.15f) continue;
            Put(fb, art.Dot[mz > 0.75f ? 0 : mz > 0.5f ? 1 : 2], _headX + mx, _headY + my);
        }
        if (HasNose)
        {
            // four pages: the two yaws either side, each at the two pitches either side
            PutMix(fb, art.Nose[yl, pl], art.Nose[yl, pl + 1], pfr, _headX, _headY);
            if (yfr > 0.02f)
            {
                PutMix(fb, art.Nose[yl + 1, pl], art.Nose[yl + 1, pl + 1], pfr, _headX, _headY, yfr);
            }
        }
        else
        {
            YardSnowmanArt.FacePoint(0, -0.06f, _yaw, _pitch, R, out float hx, out float hy, out float hz);
            if (hz > 0.2f) Put(fb, art.Hole, _headX + hx, _headY + hy);
        }

        // 5. The scarf (over the head's chin and the middle ball's top), with snow lying on it.
        float spf = (float)(_scarfPh * YardSnowmanArt.ScarfPages); int sp = (int)spf % YardSnowmanArt.ScarfPages;
        PutMix(fb, art.Scarf[sp], art.Scarf[(sp + 1) % YardSnowmanArt.ScarfPages], spf - MathF.Floor(spf), _midX, _neckY);   // the phase is added up each frame and the two pages either side are blended: no page is ever skipped
        if (_snow > 0.35f)
        {
            float so = Math.Clamp((_snow - 0.30f) * 1.8f, 0f, 1f);
            for (int j = 0; j < 5; j++)
            {
                float a = MathF.PI * (0.20f + 0.15f * j);
                Put(fb, art.Powder[j & 1], _midX - 0.150f * z.T * MathF.Cos(a), _neckY + 0.045f * z.T * MathF.Sin(a) - 0.030f * z.T, so * 0.85f);
            }
        }

        // 6. The hat.
        float ang = _hatHold ? _hatHeldAng : _hatA;
        int ha = Math.Clamp((int)MathF.Round(ang / YardSnowmanArt.HatStep) + YardSnowmanArt.HatPages / 2, 0, YardSnowmanArt.HatPages - 1);
        int hs = _snow < 0.25f ? 0 : _snow < 0.60f ? 1 : 2;
        float haf = Math.Clamp(ang / YardSnowmanArt.HatStep + YardSnowmanArt.HatPages / 2, 0f, YardSnowmanArt.HatPages - 1f);
        int hl = Math.Min((int)haf, YardSnowmanArt.HatPages - 2);
        PutMix(fb, art.Hat[hl, hs], art.Hat[hl + 1, hs], haf - hl, _hatCx, _hatCy);

        // 7. The arms, over everything (a hand raised to a face or a brim is in front of it).
        for (int i = 0; i < 2; i++) DrawArm(fb, art, i);

        // 8. The flake he is catching, then every particle.
        if (_mind.Doing == Doing.Snowflake && _flakeA > 0.02f) Put(fb, art.Flake, _flakeX, _flakeY, _flakeA);
        for (int i = 0; i < Pool; i++)
        {
            if (_pLife[i] <= 0f) continue;
            float k = _pAge[i] / _pLife[i];
            switch (_pKind[i])
            {
                case 0: Put(fb, art.Powder[(int)_pPh[i] % 3], _pX[i], _pY[i], (1f - k) * (1f - k) * 0.95f); break;
                case 1:
                    float tw = 0.55f + 0.45f * MathF.Sin(_pAge[i] * 17f + _pPh[i]);
                    Put(fb, art.Spark[(int)_pPh[i] & 1], _pX[i], _pY[i], MathF.Min(1f, k * 8f) * (1f - k * k) * tw); break;
                default:
                    int zi = k < 0.33f ? 0 : k < 0.66f ? 1 : 2;
                    Put(fb, art.Zed[zi], _pX[i], _pY[i], MathF.Sin(MathF.PI * k)); break;
            }
        }
    }

    private static readonly float[] FingerLen = [0.30f, 0.40f, 0.30f];
    private static readonly Color Bark = Color.FromArgb(104, 72, 46);
    private static readonly Color BarkLit = Color.FromArgb(164, 122, 84);

    /// <summary>A stick arm: a thick bough from the shoulder, a thinner forearm, a lit stroke on the moon side, a spur, a joint, and three twig fingers.</summary>
    private void DrawArm(FrameBuffer fb, YardSnowmanArt art, int i)
    {
        float th = _z.Thick, L = _z.ArmLen;
        PointF s = _sh[i], e = _elb[i], w = _wr[i];
        Line(fb, s, e, Bark, 1f, th * 1.05f);
        Line(fb, e, w, Bark, 1f, th * 0.86f);
        if (th >= 2.2f)
        {
            float ox = -0.22f * th, oy = -0.26f * th;
            Line(fb, new PointF(s.X + ox, s.Y + oy), new PointF(e.X + ox, e.Y + oy), BarkLit, 0.75f, th * 0.34f);
            Line(fb, new PointF(e.X + ox, e.Y + oy), new PointF(w.X + ox, w.Y + oy), BarkLit, 0.75f, th * 0.30f);
        }
        Put(fb, art.Joint, e.X, e.Y);
        float dirF = MathF.Atan2(w.Y - e.Y, w.X - e.X), b = L * 0.5f;
        // A spur: a little side shoot on the forearm, pointing up and out.
        float sx = e.X + (w.X - e.X) * 0.55f, sy = e.Y + (w.Y - e.Y) * 0.55f, sa = dirF + (i == 0 ? 1f : -1f) * 0.95f;
        Line(fb, new PointF(sx, sy), new PointF(sx + MathF.Cos(sa) * b * 0.20f, sy + MathF.Sin(sa) * b * 0.20f), Bark, 1f, th * 0.55f);
        float spread = _spread[i];
        for (int f = 0; f < 3; f++)
        {
            float fa = dirF + (f - 1) * spread, len = FingerLen[f] * b;
            PointF tip = new(w.X + MathF.Cos(fa) * len, w.Y + MathF.Sin(fa) * len);
            Line(fb, w, tip, Bark, 1f, th * 0.58f);
            // A fork at the end of each twig.
            Line(fb, tip, new PointF(tip.X + MathF.Cos(fa + 0.6f) * len * 0.35f, tip.Y + MathF.Sin(fa + 0.6f) * len * 0.35f), Bark, 1f, th * 0.42f);
        }
        Put(fb, art.Joint, w.X, w.Y);
        // Snow lying on top of the bough.
        if (_snow > 0.40f)
        {
            float a = Math.Clamp((_snow - 0.35f) * 1.5f, 0f, 1f) * 0.9f;
            float up = -0.55f * th;
            Line(fb, new PointF(s.X, s.Y + up), new PointF(e.X, e.Y + up), Color.FromArgb(244, 249, 255), a, th * 0.50f);
            Line(fb, new PointF(e.X, e.Y + up), new PointF(w.X, w.Y + up), Color.FromArgb(244, 249, 255), a, th * 0.44f);
        }
    }
}
