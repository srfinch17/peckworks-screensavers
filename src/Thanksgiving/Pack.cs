using Peckworks.Screensavers.Core;
using Peckworks.Screensavers.Core.Minds;

namespace Thanksgiving;

/// <summary>
/// A small pack of coyotes, and the rabbits they hunt, on the far lane at the back of the field. His line: "give me
/// a small pack of coyotes that runs around too. they occasionally dig for stuff. also some rabbits. they chase
/// rabbits sometimes. occasionally one coyote will catch a rabbit in the distance and they will play with its
/// carcass for a minute. it's supposed to be a tiny horrifying thing that happens way in the background to reward
/// viewers for watching."
///
/// FEYNMAN VERSION: the rabbits are always there, tiny, sitting and grazing and hopping about along the lane. The
/// pack is AWAY most of the time, beyond the edge of the picture. Every few minutes it trots in: the coyotes roam
/// about, stop, sit, dig at the ground, and when one is near a rabbit and feels like it, it gives chase at a
/// gallop while the rabbit flees in long hops. Most chases fail (the rabbit jinks and is faster over a short run,
/// and the coyote gives up); now and then one is caught. Then the catcher shakes it, drops it, and the whole pack
/// comes over to play with it for a minute: pouncing on it, tossing it in the air, two of them tugging it between
/// them, one carrying it a few steps and dropping it. Then one picks it up and they all trot off with it, and a
/// while later a new rabbit hops in from the edge. Every coyote and rabbit has a Mind from the shared kit
/// (Core/Minds) with dials dealt at birth, and the coyotes pin Threat notices as they pass, so the deer lift their
/// heads and the timid trot off, and the turkeys hurry: the whole field knows the pack is through.
///
/// THE THUMP AND THE HOWL (his ask, 2026-11-09: "make them a little more special ... more intelligent, or funnier").
/// A rabbit that sees a coyote while it sits or grazes does what a real cottontail does: it rears up tall, THUMPS one
/// hind foot on the ground twice (a puff of dust each time) and bolts. The thump is an alarm: it is posted on the
/// noticeboard (Sense.Friend, with the thumping rabbit as its "who", so only rabbits read it here), and every other rabbit
/// in earshot (12 coyote lengths) sits up tall at once and runs a moment later, BEFORE the coyote is on it; the lane
/// empties. The thump never saves the thumper itself when a chase is rolled to catch it. A rabbit that has fled does not
/// thump again for 20 s. And when a bout ends (the play is over, or the time is up) the boldest coyote on screen sits,
/// throws its muzzle at the sky and HOWLS for 3 s, faint sound arcs rising off it, and THEN the pack leaves. The howl is
/// a Sense.Loud at 0.5 U. More test aids: PECKWORKS_PACK=thump (the rabbits crowd together at the left, the pack comes
/// in from the left and no chase is rolled, so a thump is the first thing that happens, about 8 s in), PECKWORKS_PACK=howl
/// (a bout of 10 s with no chase, so the howl and the leave follow at about 16 s); the log has "thump", "hears thump"
/// and "HOWL" lines.
///
/// THE VULTURE (his ask, 2026-11-09: "if the coyotes do get a kill, and they're playing with it like dogs do and flinging it
/// around, that big vulture can swoop in and grab it and fly away with it, leaving those idiot coyotes wondering wtf even just
/// happened"). When a catch is made a die is rolled once (0.6, its own dice, so no older roll moves): if it comes up, 15 to 35 s
/// into the play a big turkey vulture (VultureArt.cs) appears high in the sky and comes down in a spiral over the lane
/// (about two and a half turns, the circle shrinking and the bird growing as it nears), WAITS in its last circle until the
/// carcass lies on the ground (not in a mouth, not in the air: it checks each time it passes the bottom of the circle), then
/// straightens into a long low glide, drops its legs and GRABS the rabbit off the ground, and climbs away with heavy flaps,
/// the rabbit swinging from its feet, slowing to a soar as it shrinks off the top edge. The pack's reaction is Doing.Baffled:
/// every coyote that was playing drops it and stands staring UP after the bird (the head's tilt follows it; a coyote that
/// has the bird behind it turns round), then the head comes down and it faces the nearest other coyote; one sits, one shakes
/// its head; THEN the usual end (the howl, the leave). While it glides low the vulture posts Sense.Threat at itself (0.3 U),
/// and the rabbits near its path thump and bolt (a stand-in coyote, _sky, moves with the bird so the rabbits' own thump and
/// flee code runs unchanged). Test aid: PECKWORKS_PACK=vulture (every chase catches, no thumps, the vulture starts 9 s after the
/// catch, certain); the log has "VULTURE ..." lines, one "vulture frame" line per frame and "coyote{i} baffled".
/// Not done: the vulture does not claim the director's "sky" (the pack cannot reach the HappeningDirector without touching the
/// scene), so the hawk happening may rarely circle at the same time.
///
/// The lane is the field's far edge, BEHIND every deer and turkey, so this is all drawn first through the field
/// stencil, after the tractor's bales, and everything nearer walks in front of it. Test aids: PECKWORKS_PACK=now
/// (the first bout at 5 s), PECKWORKS_PACK=catch (every chase is run and every chase catches), PECKWORKS_MINDLOG
/// (a line per coyote and rabbit each frame, and every event: chase, caught, play acts, leave).
/// </summary>
internal sealed class Pack
{
    // Baffled: the vulture has just taken the carcass. Interrupts: Play (its acts are dropped). Interrupted by: nothing (4 to 7 s, then
    // a Stand while the others finish; the howl waits for the last one). It posts nothing.
    private enum Doing { Away, Roam, Stand, Dig, Sit, Chase, Shake, Play, Leave, Howl, Baffled }
    private enum VState { None, Spiral, Glide, Grab, Climb }
    private enum RDoing { Sit, Graze, Hop, Flee, Dead, Gone, Alert, Thump }
    private enum Act { None, Pounce, Toss, Tug, Carry, Watch }

    private sealed class Coyote
    {
        public float X, Depth;                      // the feet: x in pixels, depth 0 (the lane) to MaxDepth (a little way into the field)
        public int Dir = 1;
        public float Bold;                          // the dial: a bold one chases more
        public Mind<Doing> Mind = new(Doing.Away);
        public Pace Pace = null!;
        public float Goal, GoalDepth;
        public int Target = -1;                     // the rabbit it is chasing
        public bool Doomed;                         // this chase ends in a catch (rolled when it starts)
        public float Clock;                         // seconds into this doing
        public bool Turning; public float TurnT;
        public Act Act; public float ActT;
        public bool Carrying;                       // the carcass is in its mouth
        public float LookT;                         // baffled: how far the head is lifted, 0 level to 1 straight at the sky (eased, so the look is fluid)
        public int Role;                            // baffled: 0 stands, 1 sits down, 2 shakes its head
        public bool Faced;                          // baffled: has turned to look at its mates
    }

    private sealed class Rabbit
    {
        public float X, Depth;
        public int Dir = 1;
        public float Timid;
        public Mind<RDoing> Mind = new(RDoing.Sit);
        public Pace Pace = null!;
        public float Goal, GoalDepth, Clock, Hops;  // Hops: the odometer in hop lengths, for the page
        public float CalmFor;                       // seconds no coyote has been near, for ending a flight
        public float ThumpCool;                     // seconds before it may thump again (20 after any flight)
        public int Thumps;                          // thumps struck so far this alarm (0, 1, 2)
        public float AlertFor;                      // how long an alerted rabbit sits tall before it bolts
        public double HeardAt = -1;                 // the board time of the last thump it answered
        public Coyote Fear = null!;                 // the coyote it is afraid of (the thump's cause), for which way to run
    }

    private const float MaxDepth = 0.13f;
    private const float Trot = 0.075f, Gallop = 0.22f;           // U per second
    private const float RabbitHop = 0.045f, RabbitFlee = 0.19f, RabbitDoomed = 0.13f;   // a fleeing rabbit nearly matches the gallop and the coyote's patience runs out; the one that is caught is the slow one (the chase is rolled, so the pace is)
    private const float TrotStride = 1.3f, GallopStride = 2.4f;   // in coyote lengths
    private const float HopLen = 0.9f, FleeHopLen = 1.6f;         // in rabbit lengths
    private const float BoutEvery = 240f, BoutFor = 75f, PlayFor = 60f, ChaseFor = 7f;
    private const float TurnFor = 0.28f;
    private const float HowlFor = 3.0f;                          // seconds the howl lasts
    private const float ThumpEnd = 1.5f;                         // the thump sequence: tall 0.4, foot up 0.4 to 0.65 then strikes, again 0.8 to 1.05, then holds tall to 1.5 and bolts
    private const float TooClose = 0.10f;                        // U: a coyote this near is no time for theatre, the rabbit just runs
    private const int HowlPages = 12;
    private const float BaffleLook = 2f;                         // seconds the baffled coyotes stare up after the bird
    private const int TrotPages = 24, GallopPages = 24, DigPages = 8, ShakePages = 6, PouncePages = 12, HopPages = 12, TurnPages = 3;
    private static readonly float[] Squash = [0.72f, 0.42f, 0.14f];

    private readonly ThanksgivingScenery _s;
    private readonly Random _rng;
    private readonly Noticeboard _board;
    private readonly StreamWriter? _log;
    private readonly bool[] _stencil;
    private readonly string? _test = Environment.GetEnvironmentVariable("PECKWORKS_PACK");
    private readonly Coyote[] _pack;
    private readonly Rabbit[] _rabbits;
    private readonly float _k, _r;                               // pixels to a coyote length, and to a rabbit length
    private readonly int _cw, _ch, _cox, _cby, _rw, _rh, _rox, _rby, _lw, _lh, _lox, _lby;
    private readonly Sprite[][] _trot = new Sprite[2][], _gallop = new Sprite[2][], _dig = new Sprite[2][], _shake = new Sprite[2][], _pounce = new Sprite[2][], _turn = new Sprite[2][];
    private const int LookPages = 6, PuzzlePages = 6;
    private readonly Sprite[][] _look = new Sprite[2][], _puzzle = new Sprite[2][];
    private readonly Sprite[] _stand = new Sprite[2], _sit = new Sprite[2], _carry = new Sprite[2], _pull = new Sprite[2];
    private readonly PointF[] _trotMouth = new PointF[TrotPages], _shakeMouth = new PointF[ShakePages];
    private readonly PointF _carryMouth, _pullMouth;
    private readonly Sprite[][] _hop = new Sprite[2][], _flee = new Sprite[2][];
    private readonly Sprite[] _rsit = new Sprite[2], _graze = new Sprite[2], _dead = new Sprite[2], _limp = new Sprite[2];
    private readonly Sprite[][] _tall = new Sprite[2][], _howl = new Sprite[2][], _wave = new Sprite[2][];   // _tall: foot down, half up, cocked; _wave: three sizes of sound arc
    private readonly int _tw, _th, _tox, _tby;                  // the tall rabbit's sheet
    private readonly Sprite _puff;                              // the dust a thump kicks up
    private readonly List<(float X, float Y, float T)> _puffs = new();
    private readonly PointF _howlTip;                           // where the muzzle points to, in coyote units, at full howl
    private readonly float _howlDirX, _howlDirY;                // the way the muzzle points
    private bool _howling; private int _howler = -1; private float _howlPostAt;
    private float _thumpRange, _hearRange;
    private double _time;
    private float _nextBout, _boutClock, _playClock, _nextThreat;
    private bool _on;                                             // the pack is in the picture (or coming)
    private int _carcass = -1;                                    // the dead rabbit, or -1
    private float _carcassX, _carcassDepth, _tossT = -1f, _tossFromX, _tossToX;   // a tossed carcass flies for 0.6 s
    private int _carrier = -1;                                    // the coyote with the carcass in its mouth

    // THE JOKE (his line, 2026-11-09: "when the wolves get the rabbit, there needs to be fake blood, that's the joke. it's a
    // hilariously horrifying tiny thing that happens in the background of a relaxing serene thanksgiving fall screensaver").
    // Cartoon blood and nothing else: a bright red splat on the lane where the rabbit is caught, a fan of droplets flung
    // from the catcher's mouth with every shake and every toss, each landing on the lane as a speck, and all the stains
    // fading away a couple of minutes later. The gore rolls its own dice, so the pack behaves exactly as it did before.
    private readonly Random _gore = new(1917);
    private readonly Random _alarm = new(1931);        // the thump alarm's own dice
    private readonly Sprite _splat, _speck, _drop;
    private readonly List<(float X, float Y, bool Big, double Born)> _stains = new();   // on the lane; Y is the ground under the middle
    private readonly List<Drop> _drops = new();                                        // in the air
    private float _dripClock;
    private struct Drop { public float X, Y, VX, VY, GroundY; }
    private const float StainFor = 100f, StainFade = 30f;                             // seconds a stain stays bright, then seconds it takes to fade

    // THE VULTURE (see the header). Its own dice, its own sprites, its own state; the rest of the pack only reads _vTaken, _v and _sky.
    private const float VTurn = 3.6f, VTurns = 2.5f, VGlideFor = 2.6f, VGrabFor = 0.5f, VCloseUp = 0.42f;   // seconds a turn takes, turns in the spiral, seconds of low glide and of grab, the glide's length in U
    private readonly Random _vdice = new(2611);
    private readonly VultureArt.Set _vp;
    private readonly Sprite[] _vLimp = new Sprite[VultureArt.Tiers];                  // the rabbit hanging limp, one size per height band
    private readonly int[] _vLimpW = new int[VultureArt.Tiers], _vLimpOx = new int[VultureArt.Tiers], _vLimpOy = new int[VultureArt.Tiers];
    private readonly Coyote _sky = new();                                             // a stand-in "coyote" whose X is the vulture's: the rabbits' thump and flee code takes a Coyote
    private VState _v;
    private float _vClock, _vAt, _vNextCheck, _vPostAt, _vPhase;
    private bool _vPlanned, _vTaken, _howlAfter, _vCarry, _vSoarNow;
    private float _vX, _vY, _vHeading, _vTopY, _vLaneY;
    private int _vHdir, _vSpin;
    private float _vA0, _vCx0, _vCy0, _vCx1, _vCy1, _vR0, _vR1, _vQ0, _vQ1, _vGx0, _vGy0, _vGx1, _vGy1, _vGx, _vGy, _vV;
    private Sprite? _vPage; private PointF _vFoot; private int _vTier;

    public Pack(ThanksgivingScenery s, int coyotes, int rabbits, Random sceneRng, Noticeboard board)
    {
        _s = s;
        _board = board;
        _rng = new Random(sceneRng.Next());         // its own dice
        _log = MindLog.Open();
        _stencil = s.OpenField;
        float u = s.U;
        // Sized by the field's one scale at the lane (ThanksgivingScenery.MetresToU): a coyote a metre long, a cottontail 0.4.
        _k = Math.Max(10f, u * s.MetresToU(0.06f) * 1.0f);
        _r = Math.Max(5f, u * s.MetresToU(0.06f) * 0.42f);
        _cw = (int)(2.2f * _k) + 4; _ch = (int)(1.35f * _k) + 4; _cox = (int)(1.0f * _k) + 2; _cby = _ch - 2;
        _rw = (int)(1.7f * _r) + 4; _rh = (int)(1.15f * _r) + 4; _rox = (int)(0.85f * _r) + 2; _rby = _rh - 2;
        _lw = (int)(0.9f * _r) + 4; _lh = (int)(1.3f * _r) + 4; _lox = _lw / 2; _lby = (int)(0.12f * _r) + 2;
        _tw = (int)(1.3f * _r) + 4; _th = (int)(1.7f * _r) + 4; _tox = (int)(0.65f * _r) + 2; _tby = _th - 2;   // the rabbit sat up tall is higher than it is long
        _thumpRange = MathF.Max(6f * _k, 0.30f * u);               // "within about 6 coyote lengths" (and never less than a bit past the old flight distance of 0.22 U, or the rabbit would bolt before it could thump)
        _hearRange = 12f * _k;                                     // "about 12 coyote lengths": how far a thump carries to other rabbits
        { var hp = CoyoteArt.Howl(0.5f); float a = hp.HeadTilt * MathF.PI / 180f; _howlDirX = MathF.Cos(a); _howlDirY = MathF.Sin(a); _howlTip = new PointF(hp.Head.X + 0.33f * _howlDirX, hp.Head.Y + 0.33f * _howlDirY); }
        for (int d = 0; d < 2; d++)
        {
            bool m = d == 1;
            Sprite C(CoyoteArt.Pose q) => CoyoteArt.Page(q, _k, _cw, _ch, _cox, _cby, m);
            _trot[d] = new Sprite[TrotPages]; _gallop[d] = new Sprite[GallopPages]; _dig[d] = new Sprite[DigPages]; _shake[d] = new Sprite[ShakePages]; _pounce[d] = new Sprite[PouncePages]; _turn[d] = new Sprite[TurnPages];
            for (int i = 0; i < TrotPages; i++) { var q = CoyoteArt.Trot(i / (float)TrotPages, TrotStride); _trot[d][i] = C(q); _trotMouth[i] = CoyoteArt.MouthPoint(q); }
            for (int i = 0; i < GallopPages; i++) _gallop[d][i] = C(CoyoteArt.Gallop(i / (float)GallopPages, GallopStride));
            for (int i = 0; i < DigPages; i++) _dig[d][i] = C(CoyoteArt.Dig(i / (float)DigPages));
            for (int i = 0; i < ShakePages; i++) { var q = CoyoteArt.Shake(28f * MathF.Sin(MathF.Tau * i / ShakePages)); _shake[d][i] = C(q); _shakeMouth[i] = CoyoteArt.MouthPoint(q); }
            for (int i = 0; i < PouncePages; i++) _pounce[d][i] = C(CoyoteArt.Pounce((i + 0.5f) / PouncePages));
            for (int i = 0; i < TurnPages; i++) _turn[d][i] = Sprite.Paint(_cw, _ch, g => { if (m) { g.TranslateTransform(_cw, 0); g.ScaleTransform(-1, 1); } g.TranslateTransform(_cox, _cby); g.ScaleTransform(_k * Squash[i], -_k); CoyoteArt.Paint(g, CoyoteArt.Stand()); });
            _stand[d] = C(CoyoteArt.Stand()); _sit[d] = C(CoyoteArt.Sit());
            var carry = CoyoteArt.Carry(); _carry[d] = C(carry); _carryMouth = CoyoteArt.MouthPoint(carry);
            var pull = CoyoteArt.Pull(); _pull[d] = C(pull); _pullMouth = CoyoteArt.MouthPoint(pull);
            Sprite R(RabbitArt.Pose q) => Sprite.Paint(_rw, _rh, g => { if (m) { g.TranslateTransform(_rw, 0); g.ScaleTransform(-1, 1); } g.TranslateTransform(_rox, _rby); g.ScaleTransform(_r, -_r); RabbitArt.Paint(g, q); });
            _hop[d] = new Sprite[HopPages]; _flee[d] = new Sprite[HopPages];
            for (int i = 0; i < HopPages; i++) { _hop[d][i] = R(RabbitArt.Hop(i / (float)HopPages)); _flee[d][i] = R(RabbitArt.Hop(i / (float)HopPages, true)); }
            _tall[d] = new Sprite[3]; _howl[d] = new Sprite[HowlPages]; _wave[d] = new Sprite[3];
            for (int i = 0; i < 3; i++) _tall[d][i] = Sprite.Paint(_tw, _th, g => { if (m) { g.TranslateTransform(_tw, 0); g.ScaleTransform(-1, 1); } g.TranslateTransform(_tox, _tby); g.ScaleTransform(_r, -_r); RabbitArt.Paint(g, RabbitArt.Tall(i * 0.5f)); });
            for (int i = 0; i < HowlPages; i++) _howl[d][i] = C(CoyoteArt.Howl((i + 0.5f) / HowlPages));
            for (int i = 0; i < 3; i++)
            {
                // A sound arc: a short curved stroke across the way the muzzle points (the cell is square, the arc centred on it),
                // three sizes so a ring seems to grow as it rises. Painted in screen coordinates (y down): "up and forward" is a negative angle.
                int ws = (int)((0.5f + 0.28f * i) * _k) + 6; float rad = (0.14f + 0.10f * i) * _k, half = 38f;
                float ang = -MathF.Atan2(_howlDirY, _howlDirX) * 180f / MathF.PI;
                _wave[d][i] = Sprite.Paint(ws, ws, g =>
                {
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    if (m) { g.TranslateTransform(ws, 0); g.ScaleTransform(-1, 1); }
                    using var pen = new Pen(Color.FromArgb(236, 230, 210), MathF.Max(1.6f, 0.05f * _k)) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round };
                    g.DrawArc(pen, ws / 2f - rad, ws / 2f - rad, rad * 2, rad * 2, ang - half, half * 2);
                });
            }
            _rsit[d] = R(RabbitArt.Sit()); _graze[d] = R(RabbitArt.Graze()); _dead[d] = R(RabbitArt.Dead());
            _limp[d] = Sprite.Paint(_lw, _lh, g => { if (m) { g.TranslateTransform(_lw, 0); g.ScaleTransform(-1, 1); } g.TranslateTransform(_lox, _lby); g.ScaleTransform(_r, -_r); RabbitArt.Paint(g, RabbitArt.Limp()); });
            _look[d] = new Sprite[LookPages]; _puzzle[d] = new Sprite[PuzzlePages];
            for (int i = 0; i < LookPages; i++) _look[d][i] = C(CoyoteArt.Look(i / (float)(LookPages - 1)));
            for (int i = 0; i < PuzzlePages; i++) _puzzle[d][i] = C(CoyoteArt.Puzzle(45f * MathF.Sin(MathF.Tau * i / PuzzlePages)));
        }
        // The vulture's pages, and the limp rabbit it carries painted at each height band's size (a bird far up is far away, and so is what hangs from it).
        _vp = new VultureArt.Set(_k);
        for (int t = 0; t < VultureArt.Tiers; t++)
        {
            float rr = _r * VultureArt.Scale(t); int lw = (int)(0.9f * rr) + 4, lh = (int)(1.3f * rr) + 4, lox = lw / 2, lby = (int)(0.12f * rr) + 2;
            _vLimp[t] = Sprite.Paint(lw, lh, g => { g.TranslateTransform(lox, lby); g.ScaleTransform(rr, -rr); RabbitArt.Paint(g, RabbitArt.Limp()); });
            _vLimpW[t] = lw; _vLimpOx[t] = lox; _vLimpOy[t] = lby;
        }

        // The dust of a thump: a flat pale tan puff with two small side puffs, drawn rising and fading for half a second.
        {
            int dw2 = (int)(1.1f * _r) + 4, dh2 = (int)(0.6f * _r) + 4;
            _puff = Sprite.Paint(dw2, dh2, g =>
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using var b = new SolidBrush(Color.FromArgb(214, 198, 164));
                g.FillEllipse(b, dw2 / 2f - 0.30f * _r, dh2 / 2f - 0.15f * _r, 0.6f * _r, 0.3f * _r);
                g.FillEllipse(b, dw2 / 2f - 0.45f * _r, dh2 / 2f - 0.05f * _r, 0.3f * _r, 0.2f * _r);
                g.FillEllipse(b, dw2 / 2f + 0.15f * _r, dh2 / 2f - 0.05f * _r, 0.3f * _r, 0.2f * _r);
            });
        }
        // The blood: one big splat (a flat blob with ragged lobes and a few satellite drops, lying on the lane, so it is
        // wider than tall), one speck where a droplet lands, and one droplet in the air. Cartoon red, deliberately: this
        // is a joke, not a wound.
        Color red = Color.FromArgb(206, 14, 24), redDark = Color.FromArgb(150, 8, 16);
        int sw = (int)(3.2f * _r) + 6, sh = (int)(1.2f * _r) + 4;
        _splat = Sprite.Paint(sw, sh, g =>
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var b = new SolidBrush(red); using var dk = new SolidBrush(redDark);
            float cx = sw / 2f, cy = sh / 2f;
            g.FillEllipse(b, cx - 1.0f * _r, cy - 0.28f * _r, 2.0f * _r, 0.56f * _r);   // the pool a shade smaller: "a tiny bit less blood"
            for (int i = 0; i < 7; i++)                                                   // ragged lobes round the rim
            {
                float a = MathF.Tau * i / 7f + 0.3f, lr = _r * (0.18f + 0.14f * (i % 3) / 2f);
                g.FillEllipse(b, cx + MathF.Cos(a) * 1.0f * _r - lr, cy + MathF.Sin(a) * 0.28f * _r - lr * 0.6f, lr * 2, lr * 1.2f);
            }
            foreach (var (dx, dy, dr) in new[] { (1.45f, -0.1f, 0.09f), (-1.4f, 0.15f, 0.08f), (1.2f, 0.35f, 0.06f), (-1.1f, -0.3f, 0.06f) })
                g.FillEllipse(b, cx + dx * _r - dr * _r, cy + dy * _r - dr * _r * 0.7f, dr * 2 * _r, dr * 1.4f * _r);   // satellite drops
            g.FillEllipse(dk, cx - 0.5f * _r, cy - 0.12f * _r, 0.8f * _r, 0.26f * _r);    // a darker pool in the middle
        });
        int pw = (int)(0.6f * _r) + 4;
        _speck = Sprite.Paint(pw, pw, g => { g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias; using var b = new SolidBrush(red); g.FillEllipse(b, pw / 2f - 0.22f * _r, pw / 2f - 0.10f * _r, 0.44f * _r, 0.20f * _r); });
        int dw = (int)(0.3f * _r) + 4;
        _drop = Sprite.Paint(dw, dw, g => { g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias; using var b = new SolidBrush(red); float dr = MathF.Max(1f, 0.11f * _r); g.FillEllipse(b, dw / 2f - dr, dw / 2f - dr, dr * 2, dr * 2); });

        float[] bold = Traits.Deal(_rng, Math.Max(1, coyotes)), timid = Traits.Deal(_rng, Math.Max(1, rabbits));
        _pack = new Coyote[coyotes];
        for (int i = 0; i < coyotes; i++)
            _pack[i] = new Coyote { X = -10 * _k, Depth = 0.02f + 0.09f * (float)_rng.NextDouble(), Bold = bold[i], Pace = new Pace(Gallop * u * 2.5f) };
        _rabbits = new Rabbit[rabbits];
        for (int i = 0; i < rabbits; i++)
        {
            var r = new Rabbit { X = s.Width * (0.05f + 0.9f * (float)_rng.NextDouble()), Depth = 0.01f + 0.11f * (float)_rng.NextDouble(), Dir = _rng.Next(2) == 0 ? 1 : -1, Timid = timid[i], Pace = new Pace(RabbitFlee * u * 4f) };
            r.Mind.Begin(RDoing.Sit, 1f + 4f * (float)_rng.NextDouble());
            _rabbits[i] = r;
        }
        // Test: PECKWORKS_PACK=thump crowds the rabbits at the left where the pack walks in, so a thump is the first event.
        if (_test == "thump") for (int i = 0; i < rabbits; i++) { _rabbits[i].X = s.Width * (0.20f + 0.035f * i); _rabbits[i].Depth = 0.04f + 0.03f * (i % 3); _rabbits[i].Dir = 1; _rabbits[i].Mind.Begin(RDoing.Sit, 30f); }
        _nextBout = _test is "now" or "catch" or "thump" or "howl" or "vulture" ? 5f : 40f + 60f * (float)_rng.NextDouble();   // the first bout comes sooner than the rest
        Log($"sizes: coyote length k {_k:F1} px, rabbit r {_r:F1} px; thump seen within {_thumpRange:F0} px, heard within {_hearRange:F0} px");
    }

    public int Coyotes => _pack.Length;

    /// <summary>Flings n droplets from (x, y) in the air, mostly the way the catcher faces (dir), to land about groundY.</summary>
    private void Spray(float x, float y, float groundY, int n, int dir)
    {
        for (int i = 0; i < n; i++)
        {
            float vx = dir * (1.5f + 6f * (float)_gore.NextDouble()) * _r + (float)(_gore.NextDouble() - 0.5) * 4f * _r;
            float vy = -(3f + 8f * (float)_gore.NextDouble()) * _r;
            _drops.Add(new Drop { X = x, Y = y, VX = vx, VY = vy, GroundY = groundY + ((float)_gore.NextDouble() - 0.5f) * 0.5f * _r });
        }
    }

    private float FeetY(float x, float depth) => _s.FieldY(x) + (_s.Ground.YAt(Math.Clamp(x, 0, _s.Width - 1)) - _s.U * 0.006f - _s.FieldY(x)) * depth;
    private void Log(string what) => _log?.WriteLine($"{_time,8:F2} pack {what}");
    private bool OnScreen(float x, float half) => x > -half && x < _s.Width + half;

    // ================================================================ living

    public void Update(float dt)
    {
        float u = _s.U, w = _s.Width;
        _time += dt;
        if (_pack.Length > 0)
        {
            if (!_on)
            {
                _nextBout -= dt;
                if (_nextBout <= 0f) BeginBout();
            }
            else
            {
                _boutClock += dt;
                _nextThreat -= dt;
                bool anyPlaying = false;
                foreach (Coyote c in _pack) if (c.Mind.Doing is Doing.Play or Doing.Shake) anyPlaying = true;
                if (_nextThreat <= 0f)
                {
                    // The pack is a threat to everything that can feel it: pinned at each coyote in the picture.
                    foreach (Coyote c in _pack)
                        if (OnScreen(c.X, 0f) && c.Mind.Doing != Doing.Away) _board.Post(Sense.Threat, new PointF(c.X, FeetY(c.X, c.Depth)), 0.42f * u, 0.9f, c);
                    _nextThreat = 0.5f;
                }
                if (anyPlaying) _playClock += dt;
                // The bout ends: the pack leaves, with the carcass if there is one, when the play is done or the time is up.
                bool anyChasing = false;
                foreach (Coyote c in _pack) if (c.Mind.Doing is Doing.Chase or Doing.Shake) anyChasing = true;
                float boutFor = _test == "howl" ? 10f : _test == "thump" ? 22f : BoutFor;
                if (_howling)
                {
                    // THE HOWL is the last thing of a bout: when it has run its three seconds the whole pack leaves.
                    if (_pack[_howler].Clock >= HowlFor)
                    {
                        _howling = false;
                        foreach (Coyote c in _pack) if (c.Mind.Doing != Doing.Leave && c.Mind.Doing != Doing.Away) BeginLeave(c);
                    }
                }
                else if (_howlAfter)
                {
                    // The vulture has been: the howl comes as soon as the last baffled coyote has finished staring.
                    bool anyBaffled = false;
                    foreach (Coyote c in _pack) if (c.Mind.Doing == Doing.Baffled) anyBaffled = true;
                    if (!anyBaffled && !anyChasing) { _howlAfter = false; BeginHowl(); }
                }
                else if (!anyChasing && _v is VState.None && ((anyPlaying && _playClock >= PlayFor) || (!anyPlaying && _boutClock >= boutFor)))
                    BeginHowl();                                          // (never while the vulture is still on its way: the play waits for it)
                bool allAway = true;
                foreach (Coyote c in _pack) if (c.Mind.Doing != Doing.Away) allAway = false;
                if (allAway)
                {
                    _on = false;
                    _nextBout = Organic.Jitter(_rng, BoutEvery);
                    if (_test is "now" or "catch" or "vulture") _nextBout = 20f;
                    if (_carcass >= 0) { _carcass = -1; _carrier = -1; }
                    if (_v != VState.None) { _v = VState.None; Log("VULTURE gone with the bout"); }
                    _vPlanned = false; _howlAfter = false;
                    // The rabbits that ran come back one at a time over the next while, the dead one last of all, as a new rabbit.
                    for (int j = 0; j < _rabbits.Length; j++) if (_rabbits[j].Mind.Doing is RDoing.Gone or RDoing.Dead) _rabbits[j].Mind.Begin(RDoing.Gone, 8f + 40f * (float)_rng.NextDouble() + (_rabbits[j].Mind.Doing == RDoing.Dead ? 60f : 0f));
                    Log($"away, next bout in {_nextBout:F0} s");
                }
            }
            for (int i = 0; i < _pack.Length; i++) StepCoyote(i, dt);
            StepVulture(dt);
        }
        for (int i = 0; i < _rabbits.Length; i++) StepRabbit(i, dt);
        for (int i = _puffs.Count - 1; i >= 0; i--) { var p = _puffs[i]; p.T += dt; if (p.T > 0.5f) _puffs.RemoveAt(i); else _puffs[i] = p; }
        if (_tossT >= 0f) { _tossT += dt; if (_tossT >= 0.6f) { _tossT = -1f; _carcassX = _tossToX; } }
        // The droplets fall under gravity (sized to the rabbit, so the arc reads at the lane's scale) and each one that
        // reaches its bit of ground becomes a speck there; the stains go when they have faded.
        float gravity = 30f * _r;
        for (int i = _drops.Count - 1; i >= 0; i--)
        {
            Drop d = _drops[i];
            d.VY += gravity * dt; d.X += d.VX * dt; d.Y += d.VY * dt;
            if (d.Y >= d.GroundY) { _stains.Add((d.X, d.GroundY, false, _time)); _drops.RemoveAt(i); }
            else _drops[i] = d;
        }
        for (int i = _stains.Count - 1; i >= 0; i--) if (_time - _stains[i].Born > StainFor + StainFade) _stains.RemoveAt(i);
    }

    private void BeginBout()
    {
        _on = true; _boutClock = 0; _playClock = 0; _vTaken = false; _howlAfter = false; _vPlanned = false;
        bool fromLeft = _rng.Next(2) == 0 || _test == "thump";
        for (int i = 0; i < _pack.Length; i++)
        {
            Coyote c = _pack[i];
            c.Dir = fromLeft ? 1 : -1;
            c.X = fromLeft ? -_k * (1.2f + 1.3f * i) : _s.Width + _k * (1.2f + 1.3f * i);
            c.Depth = 0.02f + 0.09f * (float)_rng.NextDouble();
            c.Goal = _s.Width * (0.15f + 0.7f * (float)_rng.NextDouble()); c.GoalDepth = c.Depth;
            if (_test == "thump") c.Goal = _s.Width * 0.30f;                       // the thump test walks the pack at the crowd of rabbits
            c.Mind.Begin(Doing.Roam, 30f); c.Clock = 0; c.Pace.Want = Trot * _s.U; c.Act = Act.None; c.Carrying = false;
        }
        Log($"bout: in from the {(fromLeft ? "left" : "right")}");
    }

    private void BeginLeave(Coyote c)
    {
        bool left = c.X < _s.Width / 2f;
        c.Goal = left ? -2.5f * _k : _s.Width + 2.5f * _k; c.GoalDepth = c.Depth;
        c.Mind.Begin(Doing.Leave, 60f); c.Clock = 0; c.Pace.Want = Trot * _s.U; c.Act = Act.None;
        if (_carcass >= 0 && _carrier < 0 && _tossT < 0f) { _carrier = Array.IndexOf(_pack, c); c.Carrying = true; }   // the first to leave takes it
        Log($"coyote{Array.IndexOf(_pack, c)} leaves {(left ? "left" : "right")}{(c.Carrying ? " with the carcass" : "")}");
    }

    /// <summary>
    /// THE HOWL: the boldest coyote that is in the picture sits, lifts its muzzle and howls for HowlFor seconds, and the rest of
    /// the pack stands still and waits (no chase can begin: TryChase is closed while it lasts); when it ends Update sends the pack
    /// off. Interrupters: none (a chase cannot be running, the bout end waits for that); it interrupts the play, whose acts are
    /// dropped (a carcass in a mouth is set down). If no coyote is in the picture the pack just leaves, as before.
    /// </summary>
    private void BeginHowl()
    {
        int best = -1;
        for (int i = 0; i < _pack.Length; i++)
        {
            Coyote c = _pack[i];
            if (c.Mind.Doing is Doing.Away or Doing.Leave || !OnScreen(c.X, -0.5f * _k)) continue;
            if (best < 0 || c.Bold > _pack[best].Bold) best = i;
        }
        if (best < 0) { foreach (Coyote c in _pack) if (c.Mind.Doing != Doing.Leave && c.Mind.Doing != Doing.Away) BeginLeave(c); return; }
        _howling = true; _howler = best; _howlPostAt = 0.5f;
        for (int i = 0; i < _pack.Length; i++)
        {
            Coyote c = _pack[i];
            if (c.Mind.Doing is Doing.Away or Doing.Leave) continue;
            c.Pace.Halt(); c.Act = Act.None; c.Turning = false;
            if (i == best)
            {
                if (c.Carrying) { c.Carrying = false; _carrier = -1; _carcassX = c.X + c.Dir * _carryMouth.X * _k; _carcassDepth = c.Depth; }
                c.Mind.Begin(Doing.Howl, HowlFor + 1f); c.Clock = 0;
            }
            else c.Mind.Begin(Doing.Stand, HowlFor + 1f);   // stands and listens; keeps what is in its mouth
        }
        Log($"coyote{best} HOWL (bold {_pack[best].Bold:F2}) at {_pack[best].X:F0}");
    }

    private void StartTurn(Coyote c, int dir) { if (dir == c.Dir || c.Turning) return; c.Turning = true; c.TurnT = 0; }

    private void StepCoyote(int i, float dt)
    {
        Coyote c = _pack[i];
        float u = _s.U, w = _s.Width;
        c.Mind.Tick(dt);
        c.Clock += dt;
        _log?.WriteLine($"{_time,8:F3} coyote{i} frame x {c.X:F1} y {FeetY(c.X, c.Depth):F1} v {c.Pace.Speed:F2} {c.Mind.Doing}{(c.Act != Act.None ? "/" + c.Act : "")}");
        if (c.Mind.Doing == Doing.Away) return;
        if (c.Turning)
        {
            float was = c.TurnT; c.TurnT += dt;
            if (was < TurnFor / 2 && c.TurnT >= TurnFor / 2) c.Dir = -c.Dir;
            if (c.TurnT >= TurnFor) c.Turning = false;
            return;
        }
        // Moving toward a goal: the pace eases, the body follows the heading along the lane, the depth drifts toward its goal.
        void Travel(float speed)
        {
            int toward = c.Goal >= c.X ? 1 : -1;
            if (toward != c.Dir && MathF.Abs(c.Goal - c.X) > 0.3f * _k) { StartTurn(c, toward); return; }
            c.Pace.Want = speed;
            float step = c.Pace.Tick(dt);
            c.X += c.Dir * step;
            c.Depth += Math.Clamp(c.GoalDepth - c.Depth, -0.04f * dt, 0.04f * dt);
        }
        bool Arrived() => MathF.Abs(c.Goal - c.X) < 0.25f * _k;
        switch (c.Mind.Doing)
        {
            case Doing.Roam:
                Travel(Trot * u);
                if (Arrived() || c.Mind.Over) { c.Pace.Halt(); RollIdle(c, i); }
                else TryChase(c, i);
                break;
            case Doing.Stand:
            case Doing.Sit:
            case Doing.Dig:
                if (c.Mind.Over) RollIdle(c, i);
                else if (c.Mind.Doing == Doing.Stand) TryChase(c, i);
                break;
            case Doing.Chase:
            {
                Rabbit r = c.Target >= 0 ? _rabbits[c.Target] : null!;
                if (r == null || r.Mind.Doing is RDoing.Gone or RDoing.Dead || c.Mind.Over || !OnScreen(c.X, -0.5f * _k))
                {
                    // Lost it, or gave up: stand and pant.
                    c.Target = -1; c.Pace.Halt(); c.Mind.Begin(Doing.Stand, 2.5f); c.Clock = 0; Log($"coyote{i} gives up");
                    break;
                }
                c.Goal = r.X; c.GoalDepth = r.Depth;
                Travel(Gallop * u);
                if (r.Mind.Doing != RDoing.Flee) BeginFlee(r, c.Target, c);
                float gap = (r.X - c.X) * c.Dir;                                              // ahead of the nose (positive) or behind
                if (c.Doomed && gap < 0.5f * _k && gap > -0.3f * _k && MathF.Abs(r.Depth - c.Depth) < 0.06f)
                {
                    // CAUGHT. The rabbit dies where it is; the catcher stops with it in its mouth and shakes it.
                    r.Mind.Begin(RDoing.Dead, 1f); r.Pace.Halt();
                    _carcass = c.Target; _carcassX = r.X; _carcassDepth = r.Depth; _carrier = i; c.Carrying = true;
                    c.Target = -1; c.Pace.Halt(); c.Mind.Begin(Doing.Shake, 3f); c.Clock = 0; _playClock = 0;
                    c.X = r.X - c.Dir * _carryMouth.X * _k; c.Depth = r.Depth;
                    Log($"coyote{i} CAUGHT rabbit{_carcass} at {r.X:F0}");
                    // The vulture's die, rolled once per catch on its own dice (a new roll on the pack's would move every older roll).
                    _vPlanned = _test == "vulture" || _vdice.NextDouble() < 0.6;
                    _vAt = _test == "vulture" ? 9f : 18f + 17f * (float)_vdice.NextDouble();   // seconds on the play clock (which counts from the catch; the shake is its first 3 s)
                    Log(_vPlanned ? $"vulture is coming, {_vAt:F0} s into the bout's play clock" : "no vulture this time");
                    // The joke lands: a splat on the lane under the rabbit and a fan of droplets off the catcher's mouth.
                    float feet = FeetY(r.X, r.Depth);
                    _stains.Add((r.X, feet, true, _time));
                    Spray(c.X + c.Dir * _carryMouth.X * _k, feet - _carryMouth.Y * _k, feet, 10, c.Dir);   // 10 droplets (14 before his "a tiny bit less blood", 2026-11-09)
                    foreach (Coyote o in _pack) if (o != c && o.Mind.Doing != Doing.Away) { o.Mind.Begin(Doing.Play, PlayFor + 5f); o.Clock = 0; o.Act = Act.None; o.Goal = r.X + (o.X < r.X ? -1 : 1) * (0.8f + 0.6f * (float)_rng.NextDouble()) * _k; o.GoalDepth = r.Depth + 0.02f * (float)(_rng.NextDouble() - 0.5); }
                }
                break;
            }
            case Doing.Shake:
                _dripClock += dt;
                if (_dripClock > 0.16f)
                {
                    // Every shake flings a couple more droplets off the mouth, the way a cartoon dog shakes a wet toy.
                    _dripClock = 0f;
                    int pg = (int)(c.Clock * 2.2f * ShakePages) % ShakePages;
                    float feet = FeetY(c.X, c.Depth);
                    Spray(c.X + c.Dir * _shakeMouth[pg].X * _k, feet - _shakeMouth[pg].Y * _k, feet, 1, c.Dir);
                }
                if (c.Mind.Over)
                {
                    // Drops it at its feet and the play begins.
                    c.Carrying = false; _carrier = -1; _carcassX = c.X + c.Dir * _carryMouth.X * _k; _carcassDepth = c.Depth;
                    c.Mind.Begin(Doing.Play, PlayFor + 5f); c.Clock = 0; c.Act = Act.None;
                    Log($"coyote{i} drops it at {_carcassX:F0}; the play begins");
                }
                break;
            case Doing.Play:
                StepPlay(c, i, dt);
                break;
            case Doing.Baffled:
            {
                // THE STARE. For the first two seconds the head is up after the bird and follows it (the tilt is eased, a bounded
                // rate, so it sweeps rather than snaps; a coyote with the bird behind it turns round, through the turn pages);
                // then the head comes down and it turns to look at its nearest mate; one sits, one shakes its head.
                bool birdUp = _v is VState.Grab or VState.Climb;
                if (c.Clock < BaffleLook && birdUp)
                {
                    int toward = _vX >= c.X ? 1 : -1;
                    if (toward != c.Dir && MathF.Abs(_vX - c.X) > 0.6f * _k) { StartTurn(c, toward); break; }
                    float rise = MathF.Atan2(FeetY(c.X, c.Depth) - 0.9f * _k - _vY, MathF.Abs(_vX - c.X) + 0.5f * _k) * 180f / MathF.PI;
                    float want = Math.Clamp(rise / 55f, 0.3f, 1f);
                    c.LookT += Math.Clamp(want - c.LookT, -3f * dt, 3f * dt);
                }
                else
                {
                    c.LookT = MathF.Max(0f, c.LookT - 1.5f * dt);
                    if (!c.Faced && c.LookT < 0.25f)
                    {
                        c.Faced = true;
                        Coyote? mate = null; float best = float.MaxValue;
                        foreach (Coyote o in _pack) if (o != c && o.Mind.Doing != Doing.Away && MathF.Abs(o.X - c.X) < best) { best = MathF.Abs(o.X - c.X); mate = o; }
                        if (mate != null && best > 0.3f * _k) StartTurn(c, mate.X >= c.X ? 1 : -1);
                    }
                }
                if (c.Mind.Over) { c.Mind.Begin(c.Role == 1 ? Doing.Sit : Doing.Stand, 8f); c.Clock = 0; c.LookT = 0f; }   // the one that sat stays sat until the howl
                break;
            }
            case Doing.Howl:
                // The note: the first Loud when the head is up (0.6 s), a second when the note is held (1.8 s), each pinned where the muzzle is.
                if (c.Clock >= _howlPostAt && _howlPostAt < 2f)
                {
                    _board.Post(Sense.Loud, new PointF(c.X, FeetY(c.X, c.Depth) - 0.9f * _k), 0.5f * u, 1f, c);
                    Log($"coyote{i} howls (Loud, reach {0.5f * u:F0} px)");
                    _howlPostAt += 1.2f;
                }
                break;
            case Doing.Leave:
                Travel(Trot * u);
                if (!OnScreen(c.X, 1.5f * _k) || c.Mind.Over) { c.Pace.Halt(); c.Mind.Begin(Doing.Away, 1f); if (c.Carrying) { c.Carrying = false; } }
                break;
        }
    }

    private void RollIdle(Coyote c, int i)
    {
        // What next: roam on, stand, dig, sit. A bold one roams more.
        int face = Dice.Pick(_rng, [0.35f + 0.3f * c.Bold, 0.25f, 0.18f, 0.15f]);
        switch (face)
        {
            case 0:
                c.Goal = _s.Width * (0.06f + 0.88f * (float)_rng.NextDouble()); c.GoalDepth = 0.02f + 0.09f * (float)_rng.NextDouble();
                c.Mind.Begin(Doing.Roam, 25f); break;
            case 1: c.Mind.Begin(Doing.Stand, Organic.Jitter(_rng, 2.2f)); break;
            case 2: c.Mind.Begin(Doing.Dig, 3f + 3f * (float)_rng.NextDouble()); break;
            default: c.Mind.Begin(Doing.Sit, 3f + 4f * (float)_rng.NextDouble()); break;
        }
        c.Clock = 0;
        Log($"coyote{i} {c.Mind.Doing}");
    }

    private void TryChase(Coyote c, int i)
    {
        if (_carcass >= 0 || _howling || _vTaken || _test is "thump" or "howl") return;             // one kill a bout; none while the pack is howling; none in the thump and howl tests
        for (int j = 0; j < _rabbits.Length; j++)
        {
            Rabbit r = _rabbits[j];
            if (r.Mind.Doing is RDoing.Gone or RDoing.Dead) continue;
            if (MathF.Abs(r.X - c.X) > 0.4f * _s.U) continue;
            // Once a second, the dice: a bold coyote goes more often. In the catch test it always goes.
            if (_test is not ("catch" or "vulture") && _rng.NextDouble() > (0.25 + 0.5 * c.Bold) / 60.0) continue;
            c.Target = j; c.Doomed = _test is "catch" or "vulture" || _rng.NextDouble() < 0.35;
            c.Mind.Begin(Doing.Chase, ChaseFor + 2f * (float)_rng.NextDouble()); c.Clock = 0;
            BeginFlee(r, j, c);
            Log($"coyote{i} chases rabbit{j}{(c.Doomed ? " (doomed)" : "")}");
            return;
        }
    }

    // ---- the play: the pack with the carcass for a minute ----
    private void StepPlay(Coyote c, int i, float dt)
    {
        float u = _s.U;
        float near = MathF.Abs(c.X - _carcassX);
        if (c.Act == Act.None)
        {
            // Come over if far; at hand, pick an act (a Tug needs a partner at hand too); otherwise watch a moment.
            if (near > 1.1f * _k && !c.Carrying)
            {
                c.Goal = _carcassX + (c.X < _carcassX ? -1 : 1) * 0.7f * _k; c.GoalDepth = _carcassDepth + 0.015f * (float)(_rng.NextDouble() - 0.5);
                int toward = c.Goal >= c.X ? 1 : -1;
                if (toward != c.Dir) { StartTurn(c, toward); return; }
                c.Pace.Want = Trot * u; c.X += c.Dir * c.Pace.Tick(dt); c.Depth += Math.Clamp(c.GoalDepth - c.Depth, -0.04f * dt, 0.04f * dt);
                return;
            }
            c.Pace.Halt();
            if (_carrier >= 0 && _carrier != i) { c.Act = Act.Watch; c.ActT = 1f + (float)_rng.NextDouble(); return; }   // someone has it
            int facing = _carcassX >= c.X ? 1 : -1;
            if (facing != c.Dir) { StartTurn(c, facing); return; }
            if (_tossT >= 0f) { c.Act = Act.Watch; c.ActT = 0.8f; return; }
            bool partner = false;
            foreach (Coyote o in _pack) if (o != c && o.Mind.Doing == Doing.Play && MathF.Abs(o.X - _carcassX) < 1.5f * _k && (o.X - _carcassX) * (c.X - _carcassX) < 0 && o.Act is Act.None or Act.Watch) partner = true;
            int face = Dice.Pick(_rng, [0.35f, 0.3f, partner ? 0.3f : 0f, 0.2f, 0.3f]);
            c.Act = face switch { 0 => Act.Pounce, 1 => Act.Toss, 2 => Act.Tug, 3 => Act.Carry, _ => Act.Watch };
            if (VultureClosing && c.Act is Act.Toss or Act.Carry) c.Act = Act.Watch;   // the bird is on its last turn and approach: the carcass stays on the ground until it takes it
            c.ActT = c.Act switch { Act.Pounce => 1.0f, Act.Toss => 1.6f, Act.Tug => 2.2f, Act.Carry => 2.8f, _ => 1f + 2f * (float)_rng.NextDouble() };
            c.Clock = 0;
            if (c.Act == Act.Tug)
                foreach (Coyote o in _pack) if (o != c && o.Mind.Doing == Doing.Play && MathF.Abs(o.X - _carcassX) < 1.5f * _k && (o.X - _carcassX) * (c.X - _carcassX) < 0) { o.Act = Act.Tug; o.ActT = 2.2f; o.Clock = 0; break; }
            if (c.Act == Act.Pounce) { c.X = _carcassX - c.Dir * 0.55f * _k; }
            Log($"coyote{i} {c.Act}");
            return;
        }
        c.ActT -= dt;
        switch (c.Act)
        {
            case Act.Toss:
                // Picks it up over the first half, flings it over the second: the carcass flies a short arc the way it faces.
                if (c.Clock < 0.6f) { c.X = _carcassX - c.Dir * _carryMouth.X * _k; }
                else if (_carrier < 0 && c.Clock >= 0.6f && c.Clock < 0.9f) { _carrier = i; c.Carrying = true; }
                else if (c.Carrying && c.Clock >= 0.9f) { c.Carrying = false; _carrier = -1; _tossT = 0f; _tossFromX = c.X + c.Dir * _carryMouth.X * _k; _tossToX = _tossFromX + c.Dir * (0.5f + 0.4f * (float)_rng.NextDouble()) * _k; _carcassX = _tossFromX; Spray(_tossFromX, FeetY(c.X, c.Depth) - _carryMouth.Y * _k, FeetY(c.X, c.Depth), 3, c.Dir); }
                break;
            case Act.Carry:
                if (c.Clock < 0.4f) c.X = _carcassX - c.Dir * _carryMouth.X * _k;
                else if (!c.Carrying && _carrier < 0 && c.Clock < 0.6f) { _carrier = i; c.Carrying = true; }
                else if (c.Carrying && c.Clock < 2.2f) { c.Pace.Want = Trot * 0.7f * u; c.X += c.Dir * c.Pace.Tick(dt); _carcassX = c.X + c.Dir * _trotMouth[0].X * _k; _carcassDepth = c.Depth; }
                else if (c.Carrying) { c.Carrying = false; _carrier = -1; c.Pace.Halt(); _carcassX = c.X + c.Dir * _carryMouth.X * _k; }
                break;
            case Act.Tug:
                c.X = _carcassX - c.Dir * (_pullMouth.X + 0.08f * MathF.Sin(c.Clock * 9f)) * _k;      // braced at it, heaving
                break;
        }
        if (c.ActT <= 0f) { c.Act = Act.None; c.Clock = 0; }
    }

    // ---- the vulture ----

    /// <summary>The carcass lies on the ground: nobody has it in a mouth, none is mid-toss, nobody is about to pick it up.</summary>
    private bool CarcassResting()
    {
        if (_carcass < 0 || _carrier >= 0 || _tossT >= 0f || _howling) return false;
        if (_carcassX < 0.14f * _s.Width || _carcassX > 0.86f * _s.Width) return false;   // too near an edge to be a scene (the play drifts the carcass about)
        foreach (Coyote c in _pack) if (c.Mind.Doing == Doing.Play && c.Act is Act.Toss or Act.Carry) return false;
        return true;
    }

    /// <summary>The bird's last turn and its glide: the coyotes start no new toss or carry, so the carcass comes to rest and stays (a toss or carry already running ends within 2.8 s).</summary>
    private bool VultureClosing => _v == VState.Glide || (_v == VState.Spiral && _vClock >= VTurn * (VTurns - 1f));

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;
    private static float Sm(float x) { x = Math.Clamp(x, 0f, 1f); return x * x * (3f - 2f * x); }

    /// <summary>The height band (0 highest and smallest, 4 at the lane and full size) of a bird at screen height y.</summary>
    private int TierAt(float y)
    {
        float hf = Math.Clamp((_vLaneY - y) / MathF.Max(1f, _vLaneY - _vTopY), 0f, 1f);
        return Math.Clamp((int)MathF.Round((1f - hf) * (VultureArt.Tiers - 1)), 0, VultureArt.Tiers - 1);
    }

    private void BeginVulture()
    {
        float u = _s.U, W = _s.Width, H = _s.Height;
        float gx = _carcassX;
        _vLaneY = FeetY(_carcassX, _carcassDepth); _vTopY = 0.10f * H;
        _vHdir = gx > W * 0.5f ? 1 : -1;                                   // it glides in from the side with more room, toward the carcass
        float sx = Math.Clamp(gx - _vHdir * VCloseUp * u, 0.12f * W, 0.88f * W);
        _vR1 = 0.07f * u; _vQ1 = 0.40f; float sy = _vLaneY - 0.075f * u;  // the last circle's lowest point is where the glide begins
        _vCx1 = sx; _vCy1 = sy - _vQ1 * _vR1;
        _vR0 = 0.13f * u; _vQ0 = 0.70f;                                    // the first circle: wide and nearly round (we look up at it steeply)
        float l0 = 0.9f * _k * VultureArt.Scale(0);
        _vCx0 = Math.Clamp(sx - _vHdir * 0.16f * W, _vR0 + 1.2f * l0, W - _vR0 - 1.2f * l0);
        _vCy0 = _vTopY + _vQ0 * _vR0 + 0.03f * H;
        _vSpin = -_vHdir;                                                  // at the bottom of the circle it must be moving toward the carcass
        _vA0 = MathF.PI / 2f - _vSpin * VTurns * MathF.Tau;                // so it ends its last turn at the bottom (angle 90 degrees, y down)
        _vClock = 0f; _vNextCheck = VTurn * VTurns; _vCarry = false; _vSoarNow = false; _vPhase = 0f; _vPlanned = false;
        _v = VState.Spiral;
        Log($"VULTURE appears high up and spirals down over the carcass at {gx:F0} (in from the {(_vHdir > 0 ? "left" : "right")}, {VTurns} turns)");
    }

    private void StepVulture(float dt)
    {
        float u = _s.U;
        if (_v == VState.None)
        {
            if (_vPlanned && _carcass >= 0 && !_howling && _playClock >= _vAt && _carcassX > 0.14f * _s.Width && _carcassX < 0.86f * _s.Width) BeginVulture();
            return;
        }
        _vClock += dt;
        int d = _vHdir > 0 ? 0 : 1;
        switch (_v)
        {
            case VState.Spiral:
            {
                float p = Math.Clamp(_vClock / (VTurn * VTurns), 0f, 1f);
                if (_carcass >= 0)
                {
                    // The last circle follows the carcass (the play drifts it about), so the glide is always a short straight pass.
                    float sx = Math.Clamp(_carcassX - _vHdir * VCloseUp * u, 0.12f * _s.Width, 0.88f * _s.Width);
                    _vLaneY = FeetY(_carcassX, _carcassDepth); _vCx1 = sx; _vCy1 = _vLaneY - 0.075f * u - _vQ1 * _vR1;
                }
                float a = _vA0 + _vSpin * (MathF.Tau / VTurn) * _vClock, q = Lerp(_vQ0, _vQ1, p), r = Lerp(_vR0, _vR1, p);
                _vX = Lerp(_vCx0, _vCx1, p) + r * MathF.Cos(a);
                _vY = Lerp(_vCy0, _vCy1, p) + q * r * MathF.Sin(a);
                _vHeading = MathF.Atan2(-MathF.Sin(a) * _vSpin, -MathF.Cos(a) * _vSpin) * 180f / MathF.PI; if (_vHeading < 0f) _vHeading += 360f;
                _vTier = TierAt(_vY);
                _vPage = _vp.Soar[_vTier, (int)MathF.Round(_vHeading / 10f) % VultureArt.Headings];
                if (_carcass < 0 || _vClock > 45f) { Log($"VULTURE gives up ({(_carcass < 0 ? "the carcass is gone" : "too long")})"); _v = VState.None; break; }
                if (_vClock >= _vNextCheck)
                {
                    if (CarcassResting()) BeginGlide();
                    else { _vNextCheck += VTurn; Log("VULTURE circles once more: the carcass is not on the ground"); }
                }
                break;
            }
            case VState.Glide:
            {
                float tau = Math.Clamp(_vClock / VGlideFor, 0f, 1f);
                _vX = Lerp(_vGx0, _vGx1, tau); _vY = Lerp(_vGy0, _vGy1, 1f - (1f - tau) * (1f - tau));   // sinks steeply at first, then levels out onto the carcass
                _vHeading = _vHdir > 0 ? 70f : 290f; _vTier = TierAt(_vY);
                if (tau < 0.6f) { _vPage = _vp.Glide[_vTier, d]; }
                else { _vPage = _vp.Drop[d]; _vFoot = _vp.DropFoot[d]; _vTier = VultureArt.Tiers - 1; }
                PostThreat(dt);
                if (tau >= 1f) TakeRabbit();
                break;
            }
            case VState.Grab:
            {
                // The flare: wings thrown up, feet shut on the rabbit, still moving on, slowing and lifting a little.
                float g = _vClock / VGrabFor;
                _vX += _vHdir * _vV * (1f - 0.5f * g) * dt; _vY -= 0.05f * u * dt;
                _vPage = _vp.Grab[d]; _vFoot = _vp.GrabFoot[d]; _vTier = VultureArt.Tiers - 1;
                PostThreat(dt);
                if (_vClock >= VGrabFor) { _v = VState.Climb; _vClock = 0f; _vPhase = 0f; _vSoarNow = false; Log($"VULTURE climbs away with the rabbit from {_vX:F0},{_vY:F0}"); }
                break;
            }
            case VState.Climb:
            {
                float tc = _vClock;
                _vX += _vHdir * _vV * 0.5f * (1f - 0.85f * Sm(tc / 2.0f)) * dt;
                _vY -= u * (0.05f + 0.20f * Sm(tc / 2.2f)) * dt;
                float hd = Lerp(70f, 150f, Sm(tc / 2.4f));
                int rung = hd < 90f ? 0 : hd < 130f ? 1 : 2;
                _vPhase += Lerp(1.5f, 0.8f, MathF.Min(1f, tc / 3.5f)) * dt;      // the beat slows as it gains height
                int page = (int)((_vPhase - MathF.Floor(_vPhase)) * VultureArt.FlapPhases) % VultureArt.FlapPhases;
                if (tc > 3.4f && page == 2) _vSoarNow = true;                     // settles into the soaring V at a moment when the wings are near it
                if (_vSoarNow) page = VultureArt.FlapPhases;
                _vTier = TierAt(_vY); _vHeading = hd;
                _vPage = _vp.Flap[_vTier, rung, page, d]; _vFoot = _vp.FlapFoot[_vTier, rung, page, d];
                if (_vY < -1.5f * 0.9f * _k || tc > 25f) { _v = VState.None; _vCarry = false; Log("VULTURE gone off the top of the sky"); }
                break;
            }
        }
        _sky.X = _vX;
        _log?.WriteLine($"{_time,8:F3} vulture frame x {_vX:F1} y {_vY:F1} {_v} tier {_vTier} heading {_vHeading:F0}");
    }

    private void PostThreat(float dt)
    {
        _vPostAt -= dt;
        if (_vPostAt > 0f) return;
        _board.Post(Sense.Threat, new PointF(_vX, _vY), 0.30f * _s.U, 0.7f, _sky);      // readers: the rabbits (below), the turkeys, the deer, the patch's birds and the possum if they are within 0.3 U
        _vPostAt = 0.4f;
    }

    private void BeginGlide()
    {
        int d = _vHdir > 0 ? 0 : 1;
        PointF foot = _vp.DropFoot[d];
        float gx = _carcassX, gy = FeetY(_carcassX, _carcassDepth) - 0.15f * _r;       // the middle of the rabbit
        _vGx0 = _vX; _vGy0 = _vY; _vGx1 = gx - foot.X; _vGy1 = gy - foot.Y;            // where the bird's middle is when its feet are on it
        _vV = MathF.Abs(_vGx1 - _vGx0) / VGlideFor;
        _vGx = gx; _vGy = gy;
        _v = VState.Glide; _vClock = 0f; _vPostAt = 0f;
        Log($"VULTURE straightens into a low glide at the carcass ({_vV:F0} px/s over {MathF.Abs(_vGx1 - _vGx0):F0} px)");
    }

    /// <summary>The grab: the carcass leaves the ground with the bird, and every coyote that was playing is left baffled.</summary>
    private void TakeRabbit()
    {
        _vTaken = true; _vCarry = true; _carcass = -1; _carrier = -1; _tossT = -1f; _howlAfter = true;
        _v = VState.Grab; _vClock = 0f;
        Log($"VULTURE takes the rabbit at {_vGx:F0}");
        var idx = new List<int>();
        for (int i = 0; i < _pack.Length; i++) if (_pack[i].Mind.Doing == Doing.Play) idx.Add(i);
        int sitter = idx.Count > 0 ? idx[_vdice.Next(idx.Count)] : -1, shaker = idx.Count > 1 ? idx[(idx.IndexOf(sitter) + 1 + _vdice.Next(idx.Count - 1)) % idx.Count] : idx.Count == 1 ? idx[0] : -1;
        foreach (int i in idx)
        {
            Coyote c = _pack[i];
            float dur = 4.4f + 2.6f * (float)_vdice.NextDouble();               // 4.4 to 7 s: the head shake (2.8 to 4.3 s) always fits
            c.Pace.Halt(); c.Act = Act.None; c.Turning = false; c.Carrying = false; c.LookT = 0f; c.Faced = false;
            c.Role = i == sitter ? 1 : i == shaker ? 2 : 0;
            c.Mind.Begin(Doing.Baffled, dur); c.Clock = 0f;
            Log($"coyote{i} baffled for {dur:F1} s ({(c.Role == 1 ? "will sit" : c.Role == 2 ? "will shake its head" : "just stares")})");
        }
    }

    private void DrawVulture(FrameBuffer fb)
    {
        if (_v == VState.None || _vPage == null) return;
        float fade = _v == VState.Spiral ? MathF.Min(1f, _vClock / 1.2f) : 1f;
        if (_vCarry)
        {
            // The rabbit hangs limp from the grip and sways a little, under the bird (the feet are drawn over it).
            int t = _vTier;
            float swing = 0.14f * _r * VultureArt.Scale(t) * MathF.Sin((float)_time * 4.5f);
            float fx = _vX + _vFoot.X + swing, fy = _vY + _vFoot.Y;
            _vLimp[t].Draw(fb, (int)MathF.Round(fx) - _vLimpOx[t], (int)MathF.Round(fy) - _vLimpOy[t], 1f, _stencil);
        }
        _vPage.DrawCentered(fb, _vX, _vY, fade, _stencil);
    }

    // ---- the rabbits ----
    private void BeginFlee(Rabbit r, int j, Coyote from)
    {
        r.Dir = r.X >= from.X ? 1 : -1;
        r.Goal = r.Dir > 0 ? _s.Width + 2f * _r : -2f * _r;                                 // off the edge away from it
        r.GoalDepth = Math.Clamp(r.Depth + 0.04f * (float)(_rng.NextDouble() - 0.5), 0.005f, MaxDepth);
        r.Mind.Begin(RDoing.Flee, 20f); r.Clock = 0; r.CalmFor = 0; r.ThumpCool = 20f;      // a rabbit that has fled does not thump again for 20 s
        Log($"rabbit{j} flees {(r.Dir > 0 ? "right" : "left")}");
    }

    private Coyote NearestCoyote(Rabbit r)
    {
        Coyote near = _pack[0]; float best = float.MaxValue;
        foreach (Coyote c in _pack) { float d = c.Mind.Doing == Doing.Away ? float.MaxValue : MathF.Abs(c.X - r.X); if (d < best) { best = d; near = c; } }
        return near;
    }

    /// <summary>The rabbit has seen a coyote close enough to warn the lane: it rears up tall facing it, and the sequence in StepRabbit follows.</summary>
    private void BeginThump(Rabbit r, int j, Coyote from)
    {
        r.Fear = from; r.Dir = from.X >= r.X ? 1 : -1; r.Thumps = 0;
        r.Mind.Begin(RDoing.Thump, ThumpEnd + 1f); r.Clock = 0; r.Pace.Halt();
        Log($"rabbit{j} sits tall and will thump (coyote at {MathF.Abs(from.X - r.X):F0} px)");
    }

    /// <summary>Reads the board for another rabbit's thump in earshot that this rabbit has not answered yet.</summary>
    private void HearThump(Rabbit r, int j)
    {
        foreach (Notice n in _board.Live)
        {
            if (n.Kind != Sense.Friend || n.Who is not Rabbit from || from == r || n.Posted <= r.HeardAt) continue;
            float dx = n.At.X - r.X, dy = n.At.Y - FeetY(r.X, r.Depth);
            if (MathF.Sqrt(dx * dx + dy * dy) > n.Reach) continue;
            r.HeardAt = n.Posted; r.Fear = from.Fear; r.Dir = from.Fear.X >= r.X ? 1 : -1;
            r.AlertFor = 0.3f + 0.5f * (float)_alarm.NextDouble() * (1f - r.Timid);     // the timid ones bolt first (the alarm's own dice: a new roll on the pack's would move every older roll)
            r.Mind.Begin(RDoing.Alert, r.AlertFor + 1f); r.Clock = 0; r.Pace.Halt();
            Log($"rabbit{j} hears thump from rabbit{Array.IndexOf(_rabbits, from)} ({MathF.Sqrt(dx * dx + dy * dy):F0} px away), sits tall, bolts in {r.AlertFor:F2} s");
            return;
        }
    }

    private void StepRabbit(int j, float dt)
    {
        Rabbit r = _rabbits[j];
        float u = _s.U, w = _s.Width;
        r.Mind.Tick(dt);
        r.Clock += dt;
        _log?.WriteLine($"{_time,8:F3} rabbit{j} frame x {r.X:F1} y {FeetY(r.X, r.Depth):F1} v {r.Pace.Speed:F2} {r.Mind.Doing}");
        float nearest = float.MaxValue;
        foreach (Coyote c in _pack) if (c.Mind.Doing != Doing.Away) nearest = MathF.Min(nearest, MathF.Abs(c.X - r.X));
        r.ThumpCool -= dt;
        // Listening: a rabbit at rest (or hopping) that hears another rabbit's thump sits up tall at once and bolts a moment later.
        if (r.Mind.Doing is RDoing.Sit or RDoing.Graze or RDoing.Hop) HearThump(r, j);
        // The vulture's shadow over the lane: a rabbit at rest or hopping near the low bird thumps (or, if it has just thumped or the bird is on
        // top of it, bolts), the same code the coyotes wake. _sky is the bird's stand-in so "which way to run" is away from it.
        if (_v is VState.Glide or VState.Grab && r.Mind.Doing is RDoing.Sit or RDoing.Graze or RDoing.Hop && MathF.Abs(_vX - r.X) < 0.30f * u)
        {
            _sky.X = _vX;
            Log($"rabbit{j} sees the vulture ({MathF.Abs(_vX - r.X):F0} px)");
            if (r.ThumpCool <= 0f && MathF.Abs(_vX - r.X) > TooClose * u) BeginThump(r, j, _sky); else BeginFlee(r, j, _sky);
        }
        switch (r.Mind.Doing)
        {
            case RDoing.Sit:
            case RDoing.Graze:
                if (nearest < _thumpRange && nearest > TooClose * u && r.ThumpCool <= 0f && _test is not ("catch" or "vulture")) { BeginThump(r, j, NearestCoyote(r)); break; }
                if (nearest < 0.22f * u) { BeginFlee(r, j, NearestCoyote(r)); break; }
                if (r.Mind.Over)
                {
                    int face = Dice.Pick(_rng, [0.45f, 0.3f, 0.25f + 0.2f * (1 - r.Timid)]);
                    if (face == 2) { r.Goal = Math.Clamp(r.X + (0.06f + 0.14f * (float)_rng.NextDouble()) * u * (_rng.Next(2) == 0 ? 1 : -1), 0.03f * w, 0.97f * w); r.GoalDepth = 0.01f + 0.11f * (float)_rng.NextDouble(); r.Dir = r.Goal >= r.X ? 1 : -1; r.Mind.Begin(RDoing.Hop, 10f); r.Hops = 0; }
                    else r.Mind.Begin(face == 0 ? RDoing.Graze : RDoing.Sit, 2f + 5f * (float)_rng.NextDouble());
                    r.Clock = 0;
                }
                break;
            case RDoing.Hop:
            {
                if (nearest < 0.22f * u) { Coyote near = _pack[0]; foreach (Coyote c in _pack) if (MathF.Abs(c.X - r.X) <= nearest) near = c; BeginFlee(r, j, near); break; }
                r.Pace.Want = RabbitHop * u;
                float step = r.Pace.Tick(dt);
                r.X += r.Dir * step; r.Hops += step / (HopLen * _r);
                r.Depth += Math.Clamp(r.GoalDepth - r.Depth, -0.03f * dt, 0.03f * dt);
                if (MathF.Abs(r.Goal - r.X) < 0.3f * _r || r.Mind.Over) { r.Pace.Halt(); r.Mind.Begin(RDoing.Sit, 1f + 3f * (float)_rng.NextDouble()); r.Clock = 0; }
                break;
            }
            case RDoing.Flee:
            {
                bool doomed = false;
                foreach (Coyote c in _pack) if (c.Mind.Doing == Doing.Chase && c.Target == j && c.Doomed) doomed = true;
                r.Pace.Want = (doomed ? RabbitDoomed : RabbitFlee) * u;
                float step = r.Pace.Tick(dt);
                r.X += r.Dir * step; r.Hops += step / (FleeHopLen * _r);
                // The jink: the goal depth wanders, so the line of flight weaves across the lane.
                if (_rng.NextDouble() < dt * 1.5) r.GoalDepth = Math.Clamp(r.Depth + 0.05f * (float)(_rng.NextDouble() - 0.5), 0.005f, MaxDepth);
                r.Depth += Math.Clamp(r.GoalDepth - r.Depth, -0.06f * dt, 0.06f * dt);
                if (!OnScreen(r.X, -1.5f * _r)) { r.Pace.Halt(); r.Mind.Begin(RDoing.Gone, 25f + 50f * (float)_rng.NextDouble()); Log($"rabbit{j} off the edge"); break; }
                bool chased = false;
                foreach (Coyote c in _pack) if (c.Mind.Doing == Doing.Chase && c.Target == j) chased = true;
                r.CalmFor = nearest > 0.40f * u && !chased ? r.CalmFor + dt : 0f;
                if (r.CalmFor > 1.5f) { r.Pace.Halt(); r.Mind.Begin(RDoing.Sit, 2f); r.Clock = 0; Log($"rabbit{j} stops, safe"); }
                break;
            }
            case RDoing.Thump:
            {
                // The sequence is read off the clock (see ThumpEnd): a strike lands at 0.65 s and at 1.05 s, each posts the alarm and kicks up dust.
                r.Pace.Halt();
                if (nearest < TooClose * u) { BeginFlee(r, j, r.Fear); break; }
                int due = r.Clock >= 1.05f ? 2 : r.Clock >= 0.65f ? 1 : 0;
                while (r.Thumps < due)
                {
                    r.Thumps++;
                    float fy = FeetY(r.X, r.Depth);
                    _board.Post(Sense.Friend, new PointF(r.X, fy), _hearRange, 0.6f, r);
                    _puffs.Add((r.X - r.Dir * 0.12f * _r, fy, 0f));   // behind the rabbit: the hind foot is at the tail end of the sat-up body
                    Log($"rabbit{j} thump {r.Thumps} at {r.X:F0} (heard within {_hearRange:F0} px)");
                }
                if (r.Clock >= ThumpEnd) BeginFlee(r, j, r.Fear);
                break;
            }
            case RDoing.Alert:
                r.Pace.Halt();
                if (nearest < TooClose * u || r.Clock >= r.AlertFor) BeginFlee(r, j, r.Fear);
                break;
            case RDoing.Dead:
                break;
            case RDoing.Gone:
                if (r.Mind.Over && !_on)
                {
                    // Hops back in from an edge.
                    r.Dir = _rng.Next(2) == 0 ? 1 : -1;
                    r.X = r.Dir > 0 ? -1.5f * _r : w + 1.5f * _r;
                    r.Depth = 0.01f + 0.11f * (float)_rng.NextDouble(); r.GoalDepth = r.Depth;
                    r.Goal = r.Dir > 0 ? w * (0.08f + 0.3f * (float)_rng.NextDouble()) : w * (0.62f + 0.3f * (float)_rng.NextDouble());
                    r.Mind.Begin(RDoing.Hop, 30f); r.Hops = 0; r.Clock = 0;
                    Log($"rabbit{j} hops in from the {(r.Dir > 0 ? "left" : "right")}");
                }
                break;
        }
    }

    // ================================================================ drawing

    /// <summary>Everything on the lane, far to near: the carcass, the rabbits and the coyotes by their depth.</summary>
    public void Draw(FrameBuffer fb)
    {
        var order = new List<(float Depth, int Kind, int Index)>();
        for (int i = 0; i < _rabbits.Length; i++) if (_rabbits[i].Mind.Doing is not RDoing.Gone and not RDoing.Dead) order.Add((_rabbits[i].Depth, 0, i));
        for (int i = 0; i < _pack.Length; i++) if (_pack[i].Mind.Doing != Doing.Away) order.Add((_pack[i].Depth, 1, i));
        if (_carcass >= 0 && _carrier < 0) order.Add((_carcassDepth, 2, _carcass));
        // The blood lies on the lane under everything that walks on it, bright for a while and then fading.
        foreach (var (sx, sy, big, born) in _stains)
        {
            float age = (float)(_time - born), alpha = age < StainFor ? 1f : 1f - (age - StainFor) / StainFade;
            (big ? _splat : _speck).DrawCentered(fb, sx, sy, alpha, _stencil);
        }
        order.Sort((a, b) => a.Depth.CompareTo(b.Depth));
        foreach (var (_, kind, idx) in order)
        {
            if (kind == 0) DrawRabbit(fb, _rabbits[idx]);
            else if (kind == 1) DrawCoyote(fb, _pack[idx]);
            else DrawCarcass(fb);
        }
        DrawVulture(fb);                                                              // over the coyotes: it is in the air above them
        foreach (Drop d in _drops) _drop.DrawCentered(fb, d.X, d.Y, 1f, _stencil);   // the droplets in the air, over everyone
        foreach (var (px, py, pt) in _puffs) _puff.DrawCentered(fb, px, py - 0.15f * _r - 0.35f * _r * pt / 0.5f, 0.75f * (1f - pt / 0.5f), _stencil);   // thump dust: rises a little and fades
        if (_howling && _pack[_howler].Mind.Doing == Doing.Howl) DrawHowlWaves(fb, _pack[_howler]);
    }

    private void DrawCarcass(FrameBuffer fb)
    {
        float x = _carcassX, y = FeetY(_carcassX, _carcassDepth);
        if (_tossT >= 0f)
        {
            float p = _tossT / 0.6f;
            x = _tossFromX + (_tossToX - _tossFromX) * p;
            y = FeetY(x, _carcassDepth) - 0.9f * _r * 4 * p * (1 - p) * 1.4f;
        }
        int d = _tossToX >= _tossFromX ? 0 : 1;
        _dead[d].Draw(fb, (int)MathF.Round(x) - (d == 0 ? _rox : _rw - _rox), (int)MathF.Round(y) - _rby, 1f, _stencil);
    }

    private void DrawRabbit(FrameBuffer fb, Rabbit r)
    {
        int d = r.Dir > 0 ? 0 : 1;
        Sprite page = r.Mind.Doing switch
        {
            RDoing.Hop => _hop[d][(int)((r.Hops - MathF.Floor(r.Hops)) * HopPages) % HopPages],
            RDoing.Flee => _flee[d][(int)((r.Hops - MathF.Floor(r.Hops)) * HopPages) % HopPages],
            RDoing.Graze => ((int)(r.Clock * 2) % 3 == 2) ? _rsit[d] : _graze[d],          // lifts its head now and then
            _ => _rsit[d],
        };
        if (r.Mind.Doing is RDoing.Thump or RDoing.Alert)
        {
            // Sat up tall: the thump's foot is down, half up (0.40 to 0.50 s and 0.80 to 0.90 s), cocked (to 0.65 s and 1.05 s), then strikes.
            float c = r.Clock; int foot = r.Mind.Doing == RDoing.Thump && ((c >= 0.40f && c < 0.50f) || (c >= 0.80f && c < 0.90f)) ? 1 : r.Mind.Doing == RDoing.Thump && ((c >= 0.50f && c < 0.65f) || (c >= 0.90f && c < 1.05f)) ? 2 : 0;
            _tall[d][foot].Draw(fb, (int)MathF.Round(r.X) - (d == 0 ? _tox : _tw - _tox), (int)MathF.Round(FeetY(r.X, r.Depth)) - _tby, 1f, _stencil);
            return;
        }
        page.Draw(fb, (int)MathF.Round(r.X) - (d == 0 ? _rox : _rw - _rox), (int)MathF.Round(FeetY(r.X, r.Depth)) - _rby, 1f, _stencil);
    }

    /// <summary>Faint sound arcs rising off the howler's muzzle: three rings a third of a cycle apart, each growing, drifting up and forward and fading.</summary>
    private void DrawHowlWaves(FrameBuffer fb, Coyote c)
    {
        float p = c.Clock / HowlFor;
        if (p < 0.18f || p > 0.88f) return;                                       // only while the head is up and the note is held
        int d = c.Dir > 0 ? 0 : 1;
        float y = FeetY(c.X, c.Depth);
        for (int i = 0; i < 3; i++)
        {
            float ph = (c.Clock * 0.9f + i / 3f) % 1f;                             // 0 born at the muzzle, 1 gone
            int size = ph < 0.34f ? 0 : ph < 0.67f ? 1 : 2;
            float dist = 0.10f + 0.65f * ph;                                       // coyote lengths out from the muzzle
            float x = c.X + c.Dir * (_howlTip.X + _howlDirX * dist) * _k, yy = y - (_howlTip.Y + _howlDirY * dist) * _k;
            _wave[d][size].DrawCentered(fb, x, yy, 0.7f * MathF.Sin(MathF.PI * ph), null);
        }
    }

    private void DrawCoyote(FrameBuffer fb, Coyote c)
    {
        int d = c.Dir > 0 ? 0 : 1;
        float y = FeetY(c.X, c.Depth);
        Sprite page; PointF mouth = _carryMouth;
        if (c.Turning)
        {
            float q = c.TurnT / TurnFor;
            page = _turn[d][q < 0.5f ? Math.Min(TurnPages - 1, (int)(q * 2 * TurnPages)) : Math.Min(TurnPages - 1, (int)((1 - q) * 2 * TurnPages))];
        }
        else if (!c.Pace.Still && c.Mind.Doing == Doing.Chase) page = _gallop[d][c.Pace.Page(GallopStride * _k, GallopPages)];
        else if (!c.Pace.Still) { int pg = c.Pace.Page(TrotStride * _k, TrotPages); page = _trot[d][pg]; mouth = _trotMouth[pg]; }
        else switch (c.Mind.Doing)
        {
            case Doing.Sit: page = _sit[d]; break;
            case Doing.Baffled:
                if (c.Role == 1 && c.Clock >= 3.0f && c.LookT < 0.05f) page = _sit[d];
                else if (c.Role == 2 && c.Clock >= 2.8f && c.Clock < 4.3f && c.LookT < 0.05f) page = _puzzle[d][(int)(c.Clock * 3f * PuzzlePages) % PuzzlePages];
                else page = _look[d][Math.Clamp((int)MathF.Round(c.LookT * (LookPages - 1)), 0, LookPages - 1)];
                break;
            case Doing.Howl: page = _howl[d][Math.Min(HowlPages - 1, (int)(c.Clock / HowlFor * HowlPages))]; break;
            case Doing.Dig: page = _dig[d][(int)(c.Clock * 3.0f * DigPages) % DigPages]; break;
            case Doing.Shake: { int pg = (int)(c.Clock * 2.2f * ShakePages) % ShakePages; page = _shake[d][pg]; mouth = _shakeMouth[pg]; break; }
            case Doing.Play when c.Act == Act.Pounce: page = _pounce[d][Math.Min(PouncePages - 1, (int)(c.Clock / 1.0f * PouncePages))]; break;
            case Doing.Play when c.Act == Act.Tug: page = _pull[d]; mouth = _pullMouth; break;
            case Doing.Play when c.Carrying: page = _carry[d]; break;
            default: page = c.Carrying ? _carry[d] : _stand[d]; break;
        }
        page.Draw(fb, (int)MathF.Round(c.X) - (d == 0 ? _cox : _cw - _cox), (int)MathF.Round(y) - _cby, 1f, _stencil);
        if (c.Carrying)
        {
            // The rabbit hangs from its mouth: the limp sheet's origin is the grip.
            float mx = c.X + c.Dir * mouth.X * _k, my = y - mouth.Y * _k;
            _limp[d].Draw(fb, (int)MathF.Round(mx) - (d == 0 ? _lox : _lw - _lox), (int)MathF.Round(my) - _lby, 1f, _stencil);
        }
    }
}
