using Peckworks.Screensavers.Core;
using Peckworks.Screensavers.Core.Minds;
using Peckworks.Screensavers.Core.Sakura;

namespace Christmas;

/// <summary>
/// The birds of the bird table: male and female cardinals, black-capped chickadees and (at most) one bossy blue jay.
/// They come out of the pines, land on exact spots (the tray, the roof ridge, the lamp's crossbar, the log's snow
/// cap, the tiny fir's tip, a corner branch, the snow under the table), peck, hop, sing, bathe in the snow, squabble
/// and scatter when the jay comes, and all go up at once at a threat.
///
/// FEYNMAN VERSION: each bird is a Mind (Core/Minds) with a few dials (timid, curious, hungry) and ONE doing at a
/// time. Perched, a small bird is never still: its head SNAPS between a few turned views with holds between,
/// its tail flicks now and then, it fluffs up into a ball. A flight is a straight-ish line with a bounding
/// up-and-down (a burst of flaps, then wings shut for a dip), ending in a flare that brings the feet onto the exact
/// pixel row of the perch. Every perch is checked against the painted pixels (NearDepth) so no bird floats or sinks.
///
/// SOLID TO: a bird on a perch is in front of the prop it sits on (it is stamped with that prop's nearness, so the
/// prop does not hide it, but nearer props do); ground birds are hidden by nearer props like every walker; a
/// flying bird is in front of everything but the snow. It passes over the log, the holly and the pines.
///
/// WHAT IT PINS: Movement when the whole flock flushes (reach 0.4 U, weight 0.6, once per flush; in reach of that
/// post are the hares, the mice and the snowman's Attention if they sit within 0.4 U of the table, nobody else) and
/// Food where a flung seed lands (reach 0.3 U, weight 0.5, once per seed and never within 8 s of the last; the mice
/// and the hares near the table are the ones in reach).
///
/// TEST VARIABLES: PECKWORKS_BIRDS=n (the count, 0 = none), PECKWORKS_SHEET=feeder (the pose sheet to review\sheet_feeder.png),
/// PECKWORKS_FEEDER=jay|sing|bathe|squabble|flee|hang|seed (forces that doing at about 6 s).
/// </summary>
internal sealed class FeederBirds : Resident
{
    // A NEW DOING LISTS ITS INTERRUPTERS (and what state every exit leaves):
    //  Away     : hidden in the pines. Ends on its timer (a flush sets a long one). Leaves the bird invisible; the next thing is Fly.
    //  Startle  : 0.1 to 0.35 s of "head up, crouched" before a flight (the stagger that makes a flock go up "all at once").
    //             Cannot be cut short. Ends in Fly.
    //  Fly      : ends on landing (Perch) or on reaching a pine (Away). Nothing cuts it short; reactions are not read in the air.
    //  Perch    : micro-business (head snaps, tail flick, fluff, a hop-turn, a glance up). A Threat or loud Loud cuts it (Alert, or a flush).
    //  Peck     : n quick pecks. Cut by a Freeze or Flee (goes back to Perch with Alert / flies).
    //  Hop      : a few hops to a goal on the ground or the other tray slot. Cut by Flee only.
    //  Squabble : 1.2 s lean-in. Cut by Flee only.
    //  Sing     : 3.5 s of song. Cut by Freeze or Flee.
    //  Bathe    : 2 s of flutter in the snow. Cut by Flee.
    // Every exit goes through Decide() or Begin(Perch), which write only the doing and its timer, never a pose value.
    private enum Doing { Away, Startle, Fly, Perch, Peck, Hop, Squabble, Sing, Bathe }
    private enum Spot { Tray, Roof, Lamp, Log, Fir, Branch, Ground, Story }

    private sealed class Perch
    {
        public PointF At;
        public Spot Kind;
        public float FootY;      // the prop's foot: how near the prop is (a bird on it is stamped one step nearer)
        public int Level;
        public int Owner = -1;   // the bird that holds it (one bird per perch)
    }

    private sealed class Bird
    {
        public BirdKind Kind; public int Index;
        public Mind<Doing> Mind = new(Doing.Away);
        public Attention Senses = new(0.4f, 0.4f);
        public float Timid, Bold, Lazy;
        public float X, Y; public int Dir = 1;
        public int PerchIx = -1; public bool OnGround;
        public double Clock;
        // flight
        public PointF From, To; public float T, Len; public bool Leaving, ToGround; public int DestIx = -1, LvFrom = 2, LvTo = 2;
        public int Plan;            // what a Startle turns into: 0 leave to the pines, 1 move to another perch, 2 go to the ground
        public float StartleT; public bool AvoidTable; public bool AfterFlush;
        public float Alpha = 1f;
        public float Ramp, Decel;   // a flight's lean-in (seconds) and slow-down (seconds): 0.15 off a perch, 0.34 onto one, 0 where a pine hides the bird
        public int Pine = -1;       // the pine a bird is coming out of or going into: it is drawn BEHIND that pine's outline, never faded
        public Sprite? Shown, Mid; public float MidT;   // the page last wanted, and the half-way page bridging a big pose change
        // perch business
        public int HeadPage, HeadTarget; public float HeadHold, HeadStep, FlickT, NextFlick, FluffT, TurnT, HangT, AlertT, LookUpT, Stay, SquabbleT, SquabbleDir;
        public bool TurnFlipped; public float HangTotal; public Bird? Pending;
        // hop / peck
        public float HopPh, HopFrom, HopTo, GoalX; public int PeckN; public bool Flung;
        public string? Force; public float StoryFor; public bool Story;
        // what to draw
        public Sprite? Prev, LastPage; public float Fade = 1f; public bool LastFlap, WasVisible;   // the page before this one, dissolved out over 0.06 s (no change of picture is one frame)
        public BirdBook? Book; public Sprite? Page; public float Lift; public int Near = 255; public bool Ground; public bool Visible;
        public string PageName = "";
        public float PrevX, PrevY;
        public float NoteT;
    }

    private struct Particle { public float X, Y, Vx, Vy, T, Life, Size; public int Kind; public bool On; }

    private static readonly float[] LevelScale = [0.64f, 0.77f, 0.90f];
    private static readonly float[] RealLength = [0.22f, 0.22f, 0.13f, 0.28f];     // metres, beak to tail tip
    private static readonly BirdKind[] Cast = [BirdKind.Cardinal, BirdKind.Chickadee, BirdKind.Hen, BirdKind.Chickadee, BirdKind.Jay,
        BirdKind.Cardinal, BirdKind.Chickadee, BirdKind.Hen, BirdKind.Chickadee, BirdKind.Chickadee, BirdKind.Hen, BirdKind.Cardinal, BirdKind.Chickadee, BirdKind.Cardinal, BirdKind.Hen, BirdKind.Chickadee];
    // How much each kind likes each sort of place: Tray, Roof, Lamp, Log, Fir, Branch, Ground.
    private static readonly float[][] Like =
    [
        // The TABLE is the point of these birds: the first film showed it standing empty for 16 s while one hen sat
        // on the log, because each of the six branch twigs counted as much as half a tray. The tray, roof and the snow
        // under the table now far outweigh everything else; a twig is somewhere a bird goes once in a while.
        [9f, 3.0f, 2.5f, 0.5f, 0.6f, 0.35f, 3.0f],    // male cardinal
        [9f, 1.0f, 0f, 0.6f, 0.5f, 0.25f, 5.0f],      // hen
        [9f, 2.5f, 0.3f, 0.8f, 1.6f, 0.35f, 2.0f],    // chickadee
        [9f, 6.0f, 0f, 0.3f, 0.2f, 0.25f, 1.0f],      // jay
    ];

    private readonly Bird[] _birds;
    private readonly List<Perch> _perches = [];
    private readonly BirdBook?[,] _books = new BirdBook?[4, 3];
    private readonly Sprite[] _dot = new Sprite[3];
    private readonly Sprite _seedSprite, _noteSprite;
    private readonly Particle[] _parts = new Particle[48];
    private readonly (float X, float Y, float Life)[] _seeds = new (float, float, float)[3];
    private (float X, float Y, float Vx, float Vy, float Gy, bool On) _flung;
    private readonly float _u;
    private readonly object _seedWho = new();   // a token of its own for Food posts: my own birds must notice a seed (a post by 'this' is skipped by me)
    private readonly string? _force;
    private readonly StreamWriter? _log;
    private double _time;
    private float _foodRest, _flushRest, _jayUntil;
    private bool[]? _mask;        // a screen-sized stencil, true everywhere: a pine's cone is switched off in it for a moment while a bird passes behind that pine
    private int _lastPine = -1;   // the pine PinePoint last picked (-1: the bird came from or goes off the top of the screen)
    private bool _forcedDone;
    private bool _tableBanned => _time < _jayUntil;
    private PointF _tableAt;

    public int Count => _birds.Length;
    public PointF BirdAt(int i) => i >= 0 && i < _birds.Length ? new PointF(_birds[i].X, _birds[i].Y) : PointF.Empty;

    public FeederBirds(ChristmasScenery s, int count, Random sceneRng, Noticeboard board) : base(s, sceneRng, board)
    {
        string? ov = Environment.GetEnvironmentVariable("PECKWORKS_BIRDS");
        if (!string.IsNullOrEmpty(ov) && int.TryParse(ov, out int o)) count = o;
        count = Math.Clamp(count, 0, 16);
        _u = s.U;
        _force = Environment.GetEnvironmentVariable("PECKWORKS_FEEDER");
        _log = MindLog.Open();
        _tableAt = new PointF(s.FeederFoot.X, s.FeederFoot.Y);
        _birds = new Bird[count];
        if (count == 0) { _seedSprite = _noteSprite = null!; return; }

        BuildPerches();
        _mask = new bool[S.Width * S.Height];
        Array.Fill(_mask, true);
        // Dials: dealt at birth from our own dice. Chickadees are the bold ones, the jay the boldest, cardinals middling.
        float[] d1 = Traits.Deal(Rng, count), d2 = Traits.Deal(Rng, count);
        for (int i = 0; i < count; i++)
        {
            var b = new Bird { Kind = Cast[i % Cast.Length], Index = i };
            float baseTimid = b.Kind switch { BirdKind.Chickadee => 0.12f, BirdKind.Jay => 0.05f, BirdKind.Hen => 0.55f, _ => 0.42f };
            b.Timid = Math.Clamp(baseTimid + 0.3f * d1[i], 0f, 1f);
            b.Bold = 1f - b.Timid; b.Lazy = d2[i];
            b.Senses = new Attention(b.Timid, 0.25f + 0.6f * d2[i]) { Keen = 1f, Hungry = 0.4f + 0.5f * d1[i] };
            // The first arrivals are staggered so the table fills one bird at a time; the jay holds back.
            b.Mind.Begin(Doing.Away, 0.5f + i * 1.6f + (b.Kind == BirdKind.Jay ? 14f : 0f) + (float)Rng.NextDouble());
            _birds[i] = b;
        }
        ApplyForce();
        _dot[0] = Sprite.Glow(2, Color.FromArgb(236, 243, 255)); _dot[1] = Sprite.Glow(3, Color.FromArgb(236, 243, 255)); _dot[2] = Sprite.Glow(4, Color.FromArgb(236, 243, 255));
        _noteSprite = Sprite.Glow(3, Color.FromArgb(255, 236, 190));
        _seedSprite = Sprite.Paint(7, 5, g => { using var br = new SolidBrush(Color.FromArgb(244, 206, 118)); g.FillEllipse(br, 0, 1, 6, 3); using var hi = new SolidBrush(Color.FromArgb(255, 252, 226)); g.FillEllipse(hi, 1, 1, 2, 1); });

        bool[] used = new bool[4];
        foreach (Bird b in _birds) used[(int)b.Kind] = true;
        string? sheet = Environment.GetEnvironmentVariable("PECKWORKS_SHEET");
        BuildInBackground(() =>
        {
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            var sheetBooks = new List<BirdBook>(); var sheetKinds = new List<BirdKind>();
            for (int k = 0; k < 4; k++)
            {
                if (!used[k]) continue;
                bool allLevels = k == (int)BirdKind.Cardinal || k == (int)BirdKind.Chickadee;
                for (int lv = allLevels ? 0 : 2; lv < 3; lv++)
                    _books[k, lv] = FeederBirdsArt.Build((BirdKind)k, Math.Max(5f, RealLength[k] * 1.8f * 0.1f * _u * LevelScale[lv] / 0.9f));
                sheetBooks.Add(_books[k, 2]!); sheetKinds.Add((BirdKind)k);
            }
            long t1 = System.Diagnostics.Stopwatch.GetTimestamp();
            Warm();
            if (_log != null) lock (_log) _log.WriteLine($"0.000 FeederBirds build {System.Diagnostics.Stopwatch.GetElapsedTime(t0, t1).TotalMilliseconds:0} ms, warm-up {System.Diagnostics.Stopwatch.GetElapsedTime(t1).TotalMilliseconds:0} ms");
            if (!string.IsNullOrEmpty(sheet)) FeederBirdsArt.WriteSheet([.. sheetBooks], [.. sheetKinds], Path.Combine(Environment.CurrentDirectory, "review", "sheet_feeder.png"));
        });
    }

    /// <summary>
    /// A HITCH IS A FIRST-USE COST: with tiered compilation off, each method is compiled in full the first time it is
    /// called, and the first frame that runs every doing's code at once cost 50 to 78 ms. Compiling them all here, on
    /// the build thread, moves that bill off the frame path.
    /// </summary>
    private static void Warm()
    {
        var all = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly;
        foreach (Type t in new[] { typeof(FeederBirds), typeof(Bird), typeof(FeederBirdsArt), typeof(BirdBook) })
            foreach (var m in t.GetMethods(all))
                try { if (!m.IsAbstract && !m.ContainsGenericParameters) System.Runtime.CompilerServices.RuntimeHelpers.PrepareMethod(m.MethodHandle); } catch { }
    }

    // ==================================================================
    //  THE PERCHES: each found on the painted pixels, never guessed
    // ==================================================================

    /// <summary>
    /// The first row, searching down from "up" rows above y to "down" rows below it, where a near prop's pixels begin
    /// (the row above is empty, this one and the next are prop). That is the top of the prop under x: where feet go.
    /// If nothing is found, the nominal y stands (and the log prints it). "nudge" says by how much the pixels moved it.
    /// </summary>
    private float SnapTop(float x, float y, int up, int down, out float nudge)
    {
        int px = Math.Clamp((int)MathF.Round(x), 0, S.Width - 1), W = S.Width;
        int y0 = Math.Max(1, (int)y - up), y1 = Math.Min(S.Height - 3, (int)y + down);
        nudge = 0;
        for (int yy = y0; yy <= y1; yy++)
            if (S.NearDepth[yy * W + px] != 0 && S.NearDepth[(yy + 1) * W + px] != 0 && S.NearDepth[(yy - 1) * W + px] == 0)
            { nudge = yy - y; return yy; }
        nudge = float.NaN;
        return y;
    }

    private static int LevelOf(float footScale) => footScale < 0.705f ? 0 : footScale < 0.835f ? 1 : 2;

    private void AddPerch(Spot kind, float x, float y, float footY, int up, int down, bool snap = true)
    {
        float nudge = 0f;
        float ny = snap ? SnapTop(x, y, up, down, out nudge) : y;
        if (_log != null) _log.WriteLine($"0.000 FeederBirds perch {kind} x={x:0.0} nominalY={y:0.0} y={ny:0.0} nudge={(snap ? nudge.ToString("0.0") : "-")}");
        _perches.Add(new Perch { At = new PointF(x, ny), Kind = kind, FootY = footY, Level = kind == Spot.Branch ? 2 : LevelOf(S.FieldScale(footY)) });
    }

    private void BuildPerches()
    {
        float th = S.FeederTray.Half, fy = S.FeederFoot.Y;
        AddPerch(Spot.Tray, S.FeederTray.At.X - 0.5f * th, S.FeederTray.At.Y, fy, 7, 5);
        AddPerch(Spot.Tray, S.FeederTray.At.X + 0.5f * th, S.FeederTray.At.Y, fy, 7, 5);
        AddPerch(Spot.Roof, S.FeederRoof.At.X, S.FeederRoof.At.Y, fy, 6, 6);
        AddPerch(Spot.Lamp, S.LampBar.At.X + 0.85f * S.LampBar.Half, S.LampBar.At.Y, S.LampFoot.Y, 5, 5);
        float logW = S.LogTop.Right - S.LogTop.Left;
        AddPerch(Spot.Log, S.LogTop.Left + 0.50f * logW, S.LogTop.TopY, S.LogHollow.Y + S.LogR * 0.82f, (int)(S.LogR * 0.5f) + 2, (int)(S.LogR * 0.7f) + 2);
        AddPerch(Spot.Log, S.LogTop.Left + 0.80f * logW, S.LogTop.TopY, S.LogHollow.Y + S.LogR * 0.82f, (int)(S.LogR * 0.5f) + 2, (int)(S.LogR * 0.7f) + 2);
        // The tiny fir's tip: the topmost row where the tree is at least four pixels wide.
        {
            float tipY = S.SaplingFoot.Y - S.SaplingHeight, x = S.SaplingFoot.X;
            float top = SnapTop(x, tipY, 6, (int)(S.SaplingHeight * 0.4f), out _);
            float ty = top;
            for (int dy = 0; dy < (int)(S.SaplingHeight * 0.3f); dy++)
            {
                int n = 0, row = Math.Clamp((int)top + dy, 0, S.Height - 1);
                for (int dx = -6; dx <= 6; dx++) { int px = Math.Clamp((int)x + dx, 0, S.Width - 1); if (S.NearDepth[row * S.Width + px] != 0) n++; }
                if (n >= 4) { ty = row; break; }
            }
            AddPerch(Spot.Fir, x, ty, S.SaplingFoot.Y, 0, 0, snap: false);
        }
        // Corner branches: the twig spots that really are wood with wood on both sides of the top edge (the Cardinal happening's test).
        var picked = new List<PointF>(); int leftN = 0, rightN = 0;
        int reach = Math.Max(3, (int)(_u * 0.025f)), side = Math.Max(2, (int)(_u * 0.010f));
        foreach (PointF spot in S.CornerBranchSpots)
        {
            if (spot.X < S.Width * 0.04f || spot.X > S.Width * 0.96f || spot.Y < _u * 0.06f || spot.Y > S.Height * 0.40f) continue;
            bool isLeft = spot.X < S.Width * 0.5f;
            if ((isLeft ? leftN : rightN) >= 3) continue;
            if (picked.Exists(p => MathF.Abs(p.X - spot.X) < S.Width * 0.045f && MathF.Abs(p.Y - spot.Y) < _u * 0.08f)) continue;
            int sx = (int)spot.X, sy = (int)spot.Y, bestX = -1, bestY = 0, bestD = int.MaxValue;
            for (int dx = -reach; dx <= reach; dx++)
                for (int y = sy - reach; y <= sy + reach; y++)
                {
                    if (!(IsWood(sx + dx, y) && IsWood(sx + dx, y + 1) && !IsWood(sx + dx, y - 2))) continue;
                    bool both = false;
                    for (int dy = -3; dy <= 3 && !both; dy++) both = IsWood(sx + dx - side, y + dy) && IsWood(sx + dx + side, y + dy);
                    if (!both) continue;
                    if (Math.Abs(dx) < bestD) { bestD = Math.Abs(dx); bestX = sx + dx; bestY = y; }
                    break;
                }
            if (bestX < 0) continue;
            picked.Add(spot); if (isLeft) leftN++; else rightN++;
            AddPerch(Spot.Branch, bestX, bestY + 1, bestY + 1, 0, 0, snap: false);
            if (leftN >= 3 && rightN >= 3) break;
        }
    }

    private bool IsWood(int x, int y)
    {
        if (x < 0 || y < 0 || x >= S.Width || y >= S.Height) return false;
        int i = y * S.Width + x;
        if (S.OpenOfBranches[i]) return false;
        uint c = S.Pixels[i];
        int d = Math.Abs((int)((c >> 16) & 0xFF) - 30) + Math.Abs((int)((c >> 8) & 0xFF) - 26) + Math.Abs((int)(c & 0xFF) - 40);
        return d < 60;
    }

    private BirdBook BookFor(Bird b, int lv) => _books[(int)b.Kind, _books[(int)b.Kind, lv] != null ? lv : 2]!;
    private int GroundLevel(float y) => LevelOf(S.FieldScale(y));

    // ==================================================================
    //  TEST FORCING
    // ==================================================================

    private void ApplyForce()
    {
        if (string.IsNullOrEmpty(_force)) return;
        void Go(int i, string what) { if (i < _birds.Length) { _birds[i].Force = what; _birds[i].Mind.Begin(Doing.Away, 0.4f + 0.5f * i); } }
        switch (_force)
        {
            case "jay": Go(4, "jay"); break;
            case "sing": Go(0, "sing"); break;
            case "bathe": Go(0, "bathe"); break;
            case "squabble": Go(0, "squabble"); Go(2, "squabble"); break;
            case "hang": Go(1, "hang"); break;
            case "seed": Go(0, "seed"); Go(1, "seedmate"); if (_birds.Length > 1) _birds[1].Senses.Hungry = 1f; break;
        }
    }

    // ==================================================================
    //  UPDATE
    // ==================================================================

    public override void Update(float dt)
    {
        if (_birds.Length == 0) return;
        long u0 = _firstUpdate ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
        UpdateAll(dt);
        if (_firstUpdate) { _firstUpdate = false; if (_log != null) _log.WriteLine($"{Board.Time:0.000} FeederBirds first-update {System.Diagnostics.Stopwatch.GetElapsedTime(u0).TotalMilliseconds:0.00} ms"); }
    }

    private bool _firstUpdate = true;

    private void UpdateAll(float dt)
    {
        _time += dt;
        _foodRest -= dt; _flushRest -= dt;
        TickParticles(dt);
        TickSeeds(dt);
        if (_force == "flee" && !_forcedDone && _time > 10f) { _forcedDone = true; Board.Post(Sense.Threat, _tableAt, 0.5f * _u, 1f, null); if (_log != null) _log.WriteLine($"{Board.Time:0.000} FeederBirds forced Threat"); }
        if (_force == "squabble" && !_forcedDone && _time > 7f && _birds.Length > 2 && _birds[0].PerchIx >= 0 && _birds[2].PerchIx >= 0
            && _birds[0].Mind.Doing is Doing.Perch && _birds[2].Mind.Doing is Doing.Perch)
        { _forcedDone = true; _birds[0].Force = _birds[2].Force = null; BeginSquabble(_birds[0], _birds[2]); }
        foreach (Bird b in _birds)
        {
            b.Senses.Tick(dt); b.Mind.Tick(dt); b.Clock += dt;
            b.PrevX = b.X; b.PrevY = b.Y;
            switch (b.Mind.Doing)
            {
                case Doing.Away: UpdateAway(b); break;
                case Doing.Startle: UpdateStartle(b, dt); break;
                case Doing.Fly: UpdateFly(b, dt); break;
                default: UpdateGrounded(b, dt); break;
            }
            Dissolve(b, dt);
            if (_log != null)
            {
                float sp = dt > 0 ? MathF.Sqrt((b.X - b.PrevX) * (b.X - b.PrevX) + (b.Y - b.PrevY) * (b.Y - b.PrevY)) / dt : 0f;
                _log.WriteLine($"{Board.Time:0.000} FeederBirds {b.Index} {b.Kind} x={b.X:0.0} y={b.Y:0.0} v={sp:0.0} doing={(b.Mind.Doing == Doing.Startle ? Doing.Fly : b.Mind.Doing)} page={b.PageName} perch={b.PerchIx} vis={(b.Visible ? 1 : 0)} dir={b.Dir}");
            }
        }
    }

    /// <summary>
    /// SILKY: whenever the page changes (a head turning to another view, a peck, the fluff, a crouch, the flare) the page that
    /// was showing is kept and faded out over 0.06 s (about four frames) under the new one, so no change of picture is a
    /// single frame. Two wing positions in a row are exempt: at a full beat the wing's own sweep is the motion.
    /// </summary>
    private static void Dissolve(Bird b, float dt)
    {
        bool vis = b.Visible && b.Page != null;
        if (!vis) { b.Prev = null; b.LastPage = null; b.WasVisible = false; b.Fade = 1f; return; }
        bool flap = b.Book != null && Array.IndexOf(b.Book.Flap, b.Page) >= 0;
        if (b.Page != b.LastPage)
        {
            if (b.LastPage != null && b.WasVisible && !(flap && b.LastFlap)) { b.Prev = b.LastPage; b.Fade = 0f; }
            b.LastPage = b.Page; b.LastFlap = flap;
        }
        b.WasVisible = true;
        if (b.Fade < 1f) { b.Fade = Math.Min(1f, b.Fade + dt / 0.06f); if (b.Fade >= 1f) b.Prev = null; }
    }

    private void Roll(Bird b, string what) { if (_log != null) _log.WriteLine($"{Board.Time:0.000} FeederBirds {b.Index} roll {what}"); }

    // ---------------------------------------------------------------- away and arrival

    private void UpdateAway(Bird b)
    {
        b.Visible = false;
        if (!b.Mind.Over) return;
        if (Lull && !b.Story) { b.Mind.Left = 1.5f; return; }
        if (b.Kind == BirdKind.Jay && Force(b) == null && _birds.Length > 1 && JayBlocked()) { b.Mind.Left = 4f; return; }
        BeginArrival(b);
    }

    private string? Force(Bird b) => b.Force;
    private bool JayBlocked() => false;

    private void BeginArrival(Bird b)
    {
        int p = ChoosePerch(b);
        if (p < 0) { b.Mind.Begin(Doing.Away, 6f + 4f * (float)Rng.NextDouble()); Roll(b, "arrival: nothing free"); return; }
        Perch pr = _perches[p];
        if (pr.Kind == Spot.Ground) { PointF g = PickGround(b); StartFlight(b, SourcePoint(b, g), g, false, -1, true, 2, GroundLevel(g.Y)); }
        else { pr.Owner = b.Index; StartFlight(b, SourcePoint(b, pr.At), pr.At, false, p, false, pr.Level, pr.Level); }
        Roll(b, $"arrival to {pr.Kind}");
    }

    private int ChoosePerch(Bird b)
    {
        float[] like = Like[(int)b.Kind];
        Span<float> w = stackalloc float[_perches.Count];
        string? f = b.Force; b.Force = f;
        for (int i = 0; i < _perches.Count; i++)
        {
            Perch p = _perches[i];
            float wt = p.Kind == Spot.Ground ? like[6] : like[(int)p.Kind];
            if (p.Owner >= 0 && p.Owner != b.Index) wt = 0;
            if (_tableBanned && b.Kind != BirdKind.Jay && (p.Kind is Spot.Tray or Spot.Roof or Spot.Ground)) wt = 0;
            if (f != null)
                wt = f switch
                {
                    "sing" => p.Kind == Spot.Lamp ? 1f : 0f,
                    "bathe" or "seed" => 0f,
                    "squabble" => p.Kind == Spot.Tray && (p.Owner < 0 || p.Owner == b.Index) && (b.Index == 0 ? p.At.X < S.FeederFoot.X : p.At.X > S.FeederFoot.X) ? 1f : 0f,
                    "hang" or "seedmate" => p.Kind == Spot.Tray ? 1f : 0f,
                    "jay" => p.Kind == Spot.Tray ? 1f : 0f,
                    _ => wt,
                };
            w[i] = wt;
        }
        if (f is "bathe") { for (int i = 0; i < _perches.Count; i++) if (_perches[i].Kind == Spot.Ground) w[i] = 1f; }
        if (f is "seed") { for (int i = 0; i < _perches.Count; i++) if (_perches[i].Kind == Spot.Tray && _perches[i].Owner < 0) { w[i] = 1f; break; } }
        // the ground is one "perch" that is never full: add a virtual entry through the Ground like-weight
        float groundW = like[6] * (_tableBanned && b.Kind != BirdKind.Jay ? 0f : 1f) * (f == null ? 1f : (f == "bathe" ? 1f : 0f));
        float total = groundW;
        for (int i = 0; i < w.Length; i++) total += w[i];
        if (total <= 0f) return -1;
        float roll = (float)Rng.NextDouble() * total;
        for (int i = 0; i < w.Length; i++) { roll -= w[i]; if (roll < 0f) return i; }
        if (groundW > 0f) { EnsureGroundPerch(); return _groundIx; }
        return -1;
    }

    private int _groundIx = -1;
    private void EnsureGroundPerch()
    {
        if (_groundIx >= 0) return;
        _perches.Add(new Perch { At = _tableAt, Kind = Spot.Ground, FootY = _tableAt.Y, Level = 2 });
        _groundIx = _perches.Count - 1;
    }

    /// <summary>A place on the snow near the table to hop about: on a seed if one lies there, else a free spot near the post.</summary>
    private PointF PickGround(Bird b)
    {
        for (int i = 0; i < _seeds.Length; i++)
            if (_seeds[i].Life > 0f && CanStandHere(_seeds[i].X + 0.0f, _seeds[i].Y, b)) return new PointF(_seeds[i].X, _seeds[i].Y);
        float room = 0.02f * _u;
        for (int tries = 0; tries < 12; tries++)
        {
            float x = _tableAt.X + ((float)Rng.NextDouble() * 2f - 1f) * 0.30f * _u;
            float y = _tableAt.Y + 0.006f * _u + (float)Rng.NextDouble() * 0.045f * _u;
            if (!CanStandHere(x, y, b)) continue;
            bool crowded = false;
            foreach (Bird o in _birds) if (o != b && o.OnGround && MathF.Abs(o.X - x) < room * 2f && MathF.Abs(o.Y - y) < room) crowded = true;
            if (!crowded) return new PointF(x, y);
        }
        return new PointF(_tableAt.X + 0.12f * _u, _tableAt.Y + 0.02f * _u);
    }

    private bool CanStandHere(float x, float y, Bird b) => S.CanStand(x, y, 0.012f * _u);

    /// <summary>Where a bird comes from: the boughs of a pine near the destination, or off the top of the screen on the nearer side.</summary>
    private PointF SourcePoint(Bird b, PointF dest)
    {
        if (Rng.NextDouble() < 0.2)
            return new PointF(dest.X < S.Width * 0.5f ? -0.04f * S.Width : 1.04f * S.Width, -0.05f * S.Height);
        return PinePoint(dest.X, dest);
    }

    /// <summary>True when the point q is inside (or within a bird's size of) pine p's cone.</summary>
    private bool InCone(int i, PointF q)
    {
        var p = S.Pines[i];
        if (q.Y < p.Top || q.Y > p.Foot) return false;
        return MathF.Abs(q.X - p.X) < 1.1f * Brushwork.PineHalfWidth(q.Y, p.Top, p.Foot, _u) + 0.04f * _u;
    }

    /// <summary>
    /// A point inside a pine for a bird to vanish into or appear from. "other" is the flight's far end: a pine that already
    /// covers it is NOT chosen (a bird perched on a branch in front of a pine would be hidden the moment it took off, or
    /// land out of sight). With no pine left, the bird uses the top of the screen (-1).
    /// </summary>
    private PointF PinePoint(float nearX, PointF other)
    {
        var pines = S.Pines;
        int best = -1; float bd = float.MaxValue;
        for (int i = 0; i < pines.Count; i++)
        {
            if (InCone(i, other)) continue;
            float d = MathF.Abs(pines[i].X - nearX) + (float)Rng.NextDouble() * 0.12f * S.Width;
            if (d < bd) { bd = d; best = i; }
        }
        if (best < 0) { _lastPine = -1; return new PointF(nearX < S.Width * 0.5f ? -0.04f * S.Width : 1.04f * S.Width, -0.05f * S.Height); }
        var p = pines[best];
        _lastPine = best;   // the flight that follows hides behind THIS pine's outline
        // Low on the tree and near its trunk, where the cone is wide: the whole bird (about 40 px long) must fit INSIDE the
        // outline when it gets there, or it would end its flight in plain sight and then vanish.
        float y = p.Top + (0.50f + 0.30f * (float)Rng.NextDouble()) * (p.Foot - p.Top);
        float hw = Brushwork.PineHalfWidth(y, p.Top, p.Foot, _u);
        return new PointF(p.X + ((float)Rng.NextDouble() - 0.5f) * hw * 0.6f, y);
    }

    // ---------------------------------------------------------------- flight

    private void StartFlight(Bird b, PointF from, PointF to, bool leaving, int destIx, bool toGround, int lvFrom, int lvTo)
    {
        b.From = from; b.To = to; b.Leaving = leaving; b.DestIx = destIx; b.ToGround = toGround;
        b.LvFrom = lvFrom; b.LvTo = lvTo; b.T = 0f;
        float dx = to.X - from.X, dy = to.Y - from.Y, dist = MathF.Sqrt(dx * dx + dy * dy);
        float v = (b.Kind == BirdKind.Jay ? 0.27f : 0.30f) * _u;   // the cruising speed, pixels a second
        // THE SPEED CURVE (the review: the old curve began at 1.25 times the average speed, 0 to 7 px in ONE frame, 13 to 70
        // times a bird in three minutes). A real bird leaves a perch by pushing off: it is slow for the first tenth of a
        // second while the wings open, then at full speed. So: a lean-in of 0.15 s off a perch (none when the bird comes
        // out of a pine: it is hidden behind the pine for that part), cruise, and a slow-down over the last 0.34 s onto a
        // perch (the flare). A bird leaving INTO a pine does not slow: it goes behind the outline at full speed.
        bool fromPerch = b.Visible;
        b.Ramp = fromPerch ? 0.15f : 0f;
        b.Decel = leaving ? 0f : 0.34f;
        b.Len = Math.Clamp(dist / v + 0.5f * (b.Ramp + b.Decel), 0.8f, 4.5f);   // never under 0.8 s: a flutter between two perches still reads as a flight
        if (MathF.Abs(dx) > 2f) b.Dir = dx > 0 ? 1 : -1;
        b.X = from.X; b.Y = from.Y;
        b.Mind.Begin(Doing.Fly, b.Len);
        b.Pine = (leaving || !b.Visible) ? _lastPine : -1;   // out of the pines or into them: passes BEHIND that pine, never fades
        _lastPine = -1;
        b.Alpha = 1f;
        b.Clock = 0; b.OnGround = false;
        if (b.PerchIx >= 0 && b.PerchIx != destIx) { _perches[b.PerchIx].Owner = -1; }
        b.PerchIx = -1;
        b.Story = b.Story && destIx >= 0;
    }

    /// <summary>
    /// How far along the trip a flight is, 0 to 1, at time t: a lean-in of "r" seconds (speed climbing in a straight
    /// line from nothing), a cruise, and a slow-down of "d" seconds (speed falling to nothing). Think of a bus: it pulls
    /// away gently, runs at its speed, and brakes into the stop. Speed never jumps from one frame to the next.
    /// </summary>
    private static float Travelled(float t, float len, float r, float d)
    {
        if (t <= 0f) return 0f;
        if (t >= len) return 1f;
        float cruise = len - r - d;                          // positive: len is at least 0.8 and r + d at most 0.49
        float v = 1f / (0.5f * r + cruise + 0.5f * d);       // cruise speed, in trips a second, so the whole trip comes to exactly 1
        if (t < r) return 0.5f * v * t * t / r;
        float done = 0.5f * v * r;
        if (t < r + cruise) return done + v * (t - r);
        done += v * cruise;
        float u = t - r - cruise;
        return done + v * (u - 0.5f * u * u / d);
    }

    private void UpdateFly(Bird b, float dt)
    {
        b.T += dt;
        float e = Travelled(b.T, b.Len, b.Ramp, b.Decel);
        float left = b.Len - b.T;
        float x = b.From.X + (b.To.X - b.From.X) * e, y = b.From.Y + (b.To.Y - b.From.Y) * e;
        // The bound: a small bird climbs while it flaps and dips while its wings are shut (an arch each cycle, a smooth
        // sin squared so the turn at the bottom is round, not a corner); the jay flies level with a gentle bob.
        // The amount fades in at take-off and out into the flare.
        float env = MathF.Min(1f, MathF.Min(b.T / 0.25f, MathF.Max(0f, left - 0.2f) / 0.3f));
        bool small = b.Kind != BirdKind.Jay;
        const float period = 0.46f, flapShare = 0.62f;
        float ph = (b.T % period) / period;
        float amp = 0.014f * _u;
        float sw = MathF.Sin(ph * MathF.PI);
        float bound = small ? amp * sw * sw * env : 0.25f * amp * MathF.Sin(b.T * 9f) * env;
        b.X = x; b.Y = y - bound;
        BirdBook bk = BookFor(b, e < 0.34f ? b.LvFrom : e < 0.67f ? (b.LvFrom + b.LvTo) / 2 : b.LvTo);
        b.Book = bk; b.Ground = false; b.Visible = true; b.Lift = 0f; b.Near = 255;
        b.Alpha = 1f;
        bool landing = !b.Leaving && left < 0.34f;
        if (landing)
        {
            int ix = Math.Clamp((int)((0.34f - left) / 0.34f * BirdBook.FlarePages), 0, BirdBook.FlarePages - 1);
            b.Page = bk.Flare[ix]; b.PageName = "flare" + ix;
            // the last flare pages are anchored by the front foot: the feet reach the perch exactly as the flight ends
        }
        else if (b.Ramp > 0f && b.T < 0.05f)
        {
            // THE PUSH: legs straighten and the body springs up off the perch before the wings take over (crouch, push, flap)
            b.Page = bk.Hop[1]; b.PageName = "push";
        }
        else if (!small)
        {
            int i = (int)(b.Clock * 5.5f * BirdBook.FlapPages) % BirdBook.FlapPages; b.Page = bk.Flap[i]; b.PageName = "flap" + i;
        }
        else
        {
            // A BURST OF TWO BEATS, THEN WINGS SHUT. The burst starts on page 8 (wings low, near the shut shape) and runs
            // 24 pages = two full beats, ending on page 7: so the move to and from the folded page changes the wings by a
            // sliver, never from wings-high to wings-shut in one frame.
            float u = ph / flapShare;
            bool fold = ph > flapShare && env >= 1f && left > 0.45f;
            if (fold) { b.Page = bk.Fold; b.PageName = "fold"; }
            else { int i = (BirdBook.FlapPages * 2 / 3 + (int)(u * 2f * BirdBook.FlapPages)) % BirdBook.FlapPages; b.Page = bk.Flap[i]; b.PageName = "flap" + i; }
        }
        if (b.Mind.Over) Land(b);
    }

    private void Land(Bird b)
    {
        if (b.Leaving)
        {
            b.Visible = false; b.PageName = "away";
            float baseT = b.Kind == BirdKind.Jay ? 20f : 9f;   // seconds away in the pines (11 at first: a third of every bird's time was spent unseen)
            float t = b.AfterFlush ? 5f + 14f * b.Timid + 3f * (float)Rng.NextDouble() : Organic.Jitter(Rng, baseT);
            b.AfterFlush = false; b.AvoidTable = false;
            b.Mind.Begin(Doing.Away, t);
            Roll(b, $"away for {t:0.0}");
            return;
        }
        b.X = b.To.X; b.Y = b.To.Y; b.Visible = true; b.Alpha = 1f;
        b.PerchIx = b.DestIx; b.OnGround = b.ToGround;
        b.Shown = null; b.MidT = 0f; b.Pine = -1;
        b.HeadPage = 0; b.HeadTarget = 0; b.HeadHold = 0.4f; b.NextFlick = Organic.Jitter(Rng, 3f); b.Stay = 0f;
        b.Mind.Begin(Doing.Perch, Organic.Jitter(Rng, b.Story ? b.StoryFor : 8f));
        if (b.Story) b.Mind.Left = b.StoryFor;
        Spot where = b.OnGround ? Spot.Ground : _perches[b.PerchIx].Kind;
        if (where == Spot.Lamp) b.Dir = -1;   // faces the lantern, its tail out over open sky and clear of the red bow
        if (b.Kind == BirdKind.Jay && where is Spot.Tray or Spot.Roof) ScatterTable(b);
        // landed on a seed: eat it (three pecks, TakeSeedUnder removes it on the downstroke)
        if (b.OnGround)
            for (int i = 0; i < _seeds.Length; i++)
                if (_seeds[i].Life > 0f && MathF.Abs(_seeds[i].X - b.X) < 0.03f * _u && MathF.Abs(_seeds[i].Y - b.Y) < 0.03f * _u) { b.PeckN = 6; b.Clock = 0; b.Flung = false; b.Mind.Begin(Doing.Peck, PeckLen(6)); break; }
        // forced test doings
        switch (b.Force)
        {
            case "sing": b.Mind.Begin(Doing.Sing, 6f); break;
            case "bathe": b.Mind.Begin(Doing.Bathe, 3.5f); break;
            case "hang": b.HangT = b.HangTotal = 1.6f; break;
            case "seed": b.PeckN = 6; b.Flung = false; b.Clock = 0; b.Mind.Begin(Doing.Peck, PeckLen(6)); break;
            case "squabble": b.Mind.Begin(Doing.Perch, 3.0f); break;
        }
        if (b.Force != null && b.Force != "squabble") b.Force = null;
        if (b.Force == null && false) { }
        Roll(b, $"landed on {where}");
    }

    private void ScatterTable(Bird jay)
    {
        _jayUntil = (float)_time + Organic.Jitter(Rng, 14f);
        foreach (Bird o in _birds)
        {
            if (o == jay || !o.Visible || o.Mind.Doing is Doing.Away or Doing.Fly or Doing.Startle) continue;
            bool atTable = o.OnGround || (o.PerchIx >= 0 && _perches[o.PerchIx].Kind is Spot.Tray or Spot.Roof);
            if (!atTable) continue;
            o.Plan = Rng.NextDouble() < 0.5 ? 1 : 0; o.AvoidTable = true; o.AfterFlush = true;
            BeginStartle(o, 0.05f + 0.3f * (float)Rng.NextDouble());
        }
        Roll(jay, "jay clears the table");
    }

    // ---------------------------------------------------------------- startle: crouch, then go

    private void BeginStartle(Bird b, float delay)
    {
        b.StartleT = delay; b.Pending = null;
        b.Mind.Begin(Doing.Startle, delay + 0.01f);
    }

    private void UpdateStartle(Bird b, float dt)
    {
        b.StartleT -= dt;
        b.Page = BookFor(b, b.OnGround ? GroundLevel(b.Y) : _perches[Math.Max(0, b.PerchIx)].Level).Crouch; b.PageName = "crouch";
        b.Book = BookFor(b, CurrentLevel(b));
        b.Page = b.Book.Crouch;
        if (b.StartleT > 0f) return;
        PointF here = new(b.X, b.Y);
        int lv = CurrentLevel(b);
        if (b.Plan == 1)
        {
            int p = ChoosePerchAway(b);
            if (p >= 0 && _perches[p].Kind != Spot.Ground) { _perches[p].Owner = b.Index; StartFlight(b, here, _perches[p].At, false, p, false, lv, _perches[p].Level); return; }
        }
        else if (b.Plan == 2)
        {
            PointF g = PickGround(b);
            StartFlight(b, here, g, false, -1, true, lv, GroundLevel(g.Y)); return;
        }
        PointF to; _lastPine = -1;
        if (Rng.NextDouble() < 0.25) to = new PointF(here.X < S.Width * 0.5f ? -0.05f * S.Width : 1.05f * S.Width, -0.06f * S.Height);
        else to = PinePoint(here.X, here);
        StartFlight(b, here, to, true, -1, false, lv, lv);
    }

    /// <summary>A perch AWAY from the table (the lamp, log, fir or a branch) for a bird that was scattered.</summary>
    private int ChoosePerchAway(Bird b)
    {
        float[] like = Like[(int)b.Kind];
        float total = 0f; Span<float> w = stackalloc float[_perches.Count];
        for (int i = 0; i < _perches.Count; i++)
        {
            Perch p = _perches[i];
            float wt = p.Kind is Spot.Tray or Spot.Roof or Spot.Ground or Spot.Story ? 0f : like[(int)p.Kind];
            if (p.Owner >= 0 && p.Owner != b.Index) wt = 0f;
            w[i] = wt; total += wt;
        }
        if (total <= 0f) return -1;
        float roll = (float)Rng.NextDouble() * total;
        for (int i = 0; i < w.Length; i++) { roll -= w[i]; if (roll < 0f) return i; }
        return -1;
    }

    private int CurrentLevel(Bird b) => b.OnGround ? GroundLevel(b.Y) : b.PerchIx >= 0 ? _perches[b.PerchIx].Level : 2;

    // ---------------------------------------------------------------- on a perch or the ground

    private void UpdateGrounded(Bird b, float dt)
    {
        b.Visible = true; b.Alpha = 1f;
        int lv = CurrentLevel(b);
        BirdBook bk = BookFor(b, lv);
        b.Book = bk; b.Lift = 0f; b.Ground = b.OnGround;
        b.Near = b.OnGround ? S.Nearness(b.Y) : b.PerchIx >= 0 && _perches[b.PerchIx].Kind != Spot.Branch ? Math.Min(254, S.Nearness(_perches[b.PerchIx].FootY) + 1) : 255;
        b.Stay += dt;
        b.AlertT -= dt; b.LookUpT -= dt; b.FluffT -= dt; b.HangT -= dt; b.FlickT -= dt; b.NextFlick -= dt;

        // ---- what it feels (once a frame) ----
        Reaction r = b.Senses.Check(Board, new PointF(b.X, b.Y), this, out Notice n);
        if (r != Reaction.None) React(b, r, n);
        if (b.Mind.Doing is Doing.Startle or Doing.Fly) return;

        switch (b.Mind.Doing)
        {
            case Doing.Peck: DoPeck(b, bk, dt); break;
            case Doing.Hop: DoHop(b, bk, dt); break;
            case Doing.Squabble: DoSquabble(b, bk, dt); break;
            case Doing.Sing: DoSing(b, bk, dt); break;
            case Doing.Bathe: DoBathe(b, bk, dt); break;
            default: DoPerch(b, bk, dt); break;
        }
        Bridged(b, bk, dt);
        if (b.Mind.Over && b.Mind.Doing is not (Doing.Fly or Doing.Startle or Doing.Away)) Decide(b);
    }

    /// <summary>
    /// NO ONE-FRAME POSE CHANGES: a jump between a plain stance and a big pose (the fluffed-up ball, head thrown up to
    /// sing, the alert stretch) is bridged by one half-way page held about four frames. Half a fluff, half a lift.
    /// </summary>
    private static void Bridged(Bird b, BirdBook bk, float dt)
    {
        Sprite want = b.Page!;
        if (b.MidT > 0f)
        {
            b.MidT -= dt;
            if (b.MidT > 0f) { b.Page = b.Mid; b.Shown = want; return; }
        }
        else if (b.Shown != null && b.Shown != want)
        {
            Sprite? m = Bridge(bk, b.Shown, want);
            if (m != null) { b.Mid = m; b.MidT = 0.07f; b.Page = m; b.Shown = want; return; }
        }
        b.Shown = want;
    }

    private static Sprite? Bridge(BirdBook bk, Sprite a, Sprite c)
    {
        bool fa = a == bk.Fluff, fc = c == bk.Fluff;
        bool la = a == bk.Alert || Array.IndexOf(bk.Sing, a) >= 0, lc = c == bk.Alert || Array.IndexOf(bk.Sing, c) >= 0;
        if (fa != fc && !la && !lc && a != bk.Crouch && c != bk.Crouch) return bk.FluffHalf;
        if (la != lc && !fa && !fc) return bk.LiftHalf;
        return null;
    }

    private void React(Bird b, Reaction r, Notice n)
    {
        bool busy = b.Mind.Doing is Doing.Squabble;
        if (r == Reaction.Flee || (r == Reaction.Freeze && n.Kind == Sense.Threat))
        {
            if (Lull && n.Kind != Sense.Threat) r = Reaction.Freeze;
            else { Flush(b, n); return; }
        }
        if (busy) return;
        switch (r)
        {
            case Reaction.Freeze:
                b.AlertT = 2.2f + 2f * b.Timid;
                if (b.Mind.Doing is Doing.Peck or Doing.Hop or Doing.Sing or Doing.Bathe) b.Mind.Begin(Doing.Perch, b.AlertT);
                Roll(b, $"freeze ({n.Kind})");
                break;
            case Reaction.Look:
                if (n.At.Y < b.Y - 0.12f * _u) { if (Rng.NextDouble() < 0.85) return; b.LookUpT = 1.1f; Roll(b, $"glance up ({n.Kind})"); }
                else { b.HeadTarget = (n.At.X - b.X) * b.Dir > 0 ? 0 : 8; b.HeadHold = 1.2f; Roll(b, $"look ({n.Kind})"); }
                break;
            case Reaction.Approach:
                if (!Lull && n.Kind == Sense.Food && !b.OnGround && b.Mind.Doing == Doing.Perch)
                {
                    b.Plan = 2; BeginStartle(b, 0.05f + 0.2f * (float)Rng.NextDouble());
                    Roll(b, "approach food");
                }
                break;
        }
    }

    /// <summary>All up at once: every bird that is showing goes into the pines after its own short delay; the bold ones come back first.</summary>
    private void Flush(Bird cause, Notice n)
    {
        if (_flushRest > 0f) return;
        _flushRest = 6f;
        foreach (Bird o in _birds)
        {
            if (!o.Visible || o.Mind.Doing is Doing.Away or Doing.Fly or Doing.Startle) continue;
            o.Plan = 0; o.AfterFlush = true;
            BeginStartle(o, 0.03f + 0.28f * (float)Rng.NextDouble());
        }
        Board.Post(Sense.Movement, new PointF(cause.X, cause.Y), 0.4f * _u, 0.6f, this);
        Roll(cause, $"flush because of {n.Kind}");
    }

    // ---- Perch: never still ----
    private void DoPerch(Bird b, BirdBook bk, float dt)
    {
        // A hop-turn: crouch, hop up, flip in the air, land (a bird turns about by hopping, never in one frame).
        if (b.Pending != null && b.TurnT <= 0f) { Bird v = b.Pending; b.Pending = null; BeginSquabble(b, v); return; }
        if (b.TurnT > 0f)
        {
            b.TurnT -= dt;
            float k = 1f - b.TurnT / 0.30f;
            if (k > 0.5f && !b.TurnFlipped) { b.Dir = -b.Dir; b.TurnFlipped = true; }
            b.Page = k < 0.22f ? bk.Hop[0] : k < 0.78f ? bk.Hop[1] : bk.Hop[2];
            b.Lift = k >= 0.22f && k < 0.78f ? 0.22f * bk.K * MathF.Sin((k - 0.22f) / 0.56f * MathF.PI) : 0f;
            b.PageName = "turnhop";
            return;
        }
        if (b.AlertT > 0f) { b.Page = bk.Alert; b.PageName = "alert"; return; }
        if (b.LookUpT > 0f) { b.Page = bk.Sing[0]; b.PageName = "lookup"; return; }
        if (b.HangT > 0f)
        {
            // a hop to swing under the edge, hanging, a hop back up: never a one-frame flip
            if (b.HangT > b.HangTotal - 0.12f || b.HangT < 0.12f) { b.Page = bk.Hop[0]; b.PageName = "hangswing"; }
            else { b.Page = bk.Hang[(int)(b.Clock * 1.1f) % 2]; b.PageName = "hang"; }
            return;
        }
        if (b.FluffT > 0f) { b.Page = bk.Fluff; b.PageName = "fluff"; return; }
        if (b.FlickT > 0f) { float fe = MathF.Sin(MathF.PI * Math.Clamp(1f - b.FlickT / 0.4f, 0f, 1f)); int fi = Math.Min(4, (int)(fe * 4.99f)); b.Page = bk.Flick[fi]; b.PageName = "flick" + fi; return; }   // the tail rises and falls through five pages on one swell
        if (b.NextFlick <= 0f) { b.FlickT = 0.4f; b.NextFlick = Organic.Jitter(Rng, 4.5f); return; }
        // Head snaps: hold a view, then flick to another through one mid view (about 0.05 s a page).
        b.HeadHold -= dt;
        if (b.HeadHold <= 0f && b.HeadPage == b.HeadTarget)
        {
            double roll = Rng.NextDouble();
            b.HeadTarget = roll < 0.5 ? 0 : roll < 0.75 ? 4 : roll < 0.9 ? 6 : 8;
            b.HeadHold = Organic.Jitter(Rng, b.HeadTarget == 0 ? 1.1f : 0.8f);
        }
        if (b.HeadPage != b.HeadTarget)
        {
            b.HeadStep -= dt;
            if (b.HeadStep <= 0f) { b.HeadPage += Math.Sign(b.HeadTarget - b.HeadPage); b.HeadStep = 0.028f; }
        }
        b.Page = bk.Head[b.HeadPage]; b.PageName = "head" + b.HeadPage;
    }

    // ---- Peck: a quick down-up-down at the seed; sometimes a seed is flung off the tray ----
    private void DoPeck(Bird b, BirdBook bk, float dt)
    {
        const float cycle = 0.42f;
        if (b.Clock < 0f) b.Clock = 0f;
        // A FEEDING BOUT: three pecks, then a short head-up look round (the way a small bird feeds: peck, peck, peck, look),
        // over and over. The look is the head turning through its views, never a still frame held.
        float cb = (float)(b.Clock % (3 * cycle + 0.7f));
        if (cb >= 3 * cycle)
        {
            float p = cb - 3 * cycle;
            int hp = p < 0.25f ? 0 : p < 0.33f ? 1 : p < 0.50f ? 2 : p < 0.58f ? 1 : 0;
            b.Page = bk.Head[hp * 2]; b.PageName = "watch" + hp; b.Flung = false;
            return;
        }
        float c = cb % cycle;
        // THE PECK as one smooth dip (down 0.14 s eased, the seed, up 0.08 s eased): four pages of depth, dissolved into each other
        float dep = c < 0.14f ? 0f : c < 0.28f ? Organic.Smooth((c - 0.14f) / 0.14f) : c < 0.34f ? 1f : c < 0.42f ? 1f - Organic.Smooth((c - 0.34f) / 0.08f) : 0f;
        if (dep <= 0.02f) { b.Page = bk.Head[0]; b.PageName = "stand"; b.Flung = false; }
        else { int pi = Math.Min(3, (int)(dep * 3.99f)); b.Page = bk.Peck[pi]; b.PageName = "peck" + pi; }
        if (c >= 0.20f && c < 0.30f && !b.Flung && !b.OnGround && b.PerchIx >= 0 && _perches[b.PerchIx].Kind == Spot.Tray && _foodRest <= 0f && !_flung.On && b.Clock > 0.3f && (Rng.NextDouble() < 0.12 || b.Force == "seed"))
        { b.Flung = true; FlingSeed(b); }
        if (b.OnGround && c >= 0.20f && c < 0.30f) { TakeSeedUnder(b); }
    }

    /// <summary>How long n pecks last with the head-up looks between each three: 0.42 s a peck and 0.7 s a look.</summary>
    private static float PeckLen(int n) => n * 0.42f + (n / 3) * 0.7f;

    private void TakeSeedUnder(Bird b)
    {
        for (int i = 0; i < _seeds.Length; i++)
            if (_seeds[i].Life > 0f && MathF.Abs(_seeds[i].X - b.X) < 0.02f * _u && MathF.Abs(_seeds[i].Y - b.Y) < 0.02f * _u) _seeds[i].Life = 0f;
    }

    private void FlingSeed(Bird b)
    {
        float side = b.Dir * (0.5f + (float)Rng.NextDouble());
        float m = 0.1f * _u * 0.9f;
        _flung = (b.X + b.Dir * 0.2f * m, b.Y - 0.2f * m, side * 0.45f * m, -1.6f * m, 6.5f * m, true);
        Roll(b, "flings a seed");
    }

    private void TickSeeds(float dt)
    {
        for (int i = 0; i < _seeds.Length; i++) if (_seeds[i].Life > 0f) _seeds[i].Life -= dt;
        if (!_flung.On) return;
        var f = _flung;
        f.Vy += f.Gy * dt; f.X += f.Vx * dt; f.Y += f.Vy * dt;
        float groundY = _tableAt.Y + 0.012f * _u;
        if (f.Y >= groundY && f.Vy > 0f)
        {
            f.On = false;
            for (int i = 0; i < _seeds.Length; i++)
                if (_seeds[i].Life <= 0f) { _seeds[i] = (f.X, groundY, 40f); break; }
            if (_foodRest <= 0f) { Board.Post(Sense.Food, new PointF(f.X, groundY), 0.3f * _u, 0.5f, _seedWho); _foodRest = 8f; }
            Spawn(f.X, groundY, 3, 0);
        }
        _flung = f;
    }

    // ---- Hop: a few hops to a goal (the other tray slot, a seed, a spot on the snow) ----
    private void DoHop(Bird b, BirdBook bk, float dt)
    {
        if (b.TurnT > 0f) { DoPerch(b, bk, dt); return; }
        float len = RealLength[(int)b.Kind] * 1.8f * 0.1f * _u * LevelScale[CurrentLevel(b)];
        float step = 0.34f * len;
        float dx = b.GoalX - b.X;
        if (b.HopPh <= 0f)
        {
            if (MathF.Abs(dx) < step * 0.6f) { FinishHop(b); return; }
            int want = dx > 0 ? 1 : -1;
            if (want != b.Dir) { b.TurnT = 0.30f; b.TurnFlipped = false; return; }
            float tx = b.X + want * step;
            if (b.OnGround && !CanStandHere(tx, b.Y, b)) { FinishHop(b); return; }
            b.HopFrom = b.X; b.HopTo = tx; b.HopPh = 0.0001f;
        }
        b.HopPh += dt / 0.30f;
        float p = b.HopPh;
        if (p < 0.22f) { b.Page = bk.Hop[0]; b.PageName = "hop0"; }
        else if (p < 0.80f)
        {
            float k = (p - 0.22f) / 0.58f;
            b.X = b.HopFrom + (b.HopTo - b.HopFrom) * Organic.Smooth(k);
            b.Lift = 0.20f * bk.K * MathF.Sin(k * MathF.PI);
            b.Page = bk.Hop[1]; b.PageName = "hop1";
        }
        else { b.X = b.HopTo; b.Page = bk.Hop[2]; b.PageName = "hop2"; }
        if (p >= 1f) b.HopPh = 0f;
    }

    private void FinishHop(Bird b)
    {
        b.HopPh = 0f;
        // The other tray slot was the goal: the bird now holds it.
        if (!b.OnGround && b.PerchIx >= 0)
            for (int i = 0; i < _perches.Count; i++)
            {
                Perch p = _perches[i];
                if (p.Kind == _perches[b.PerchIx].Kind && p.Owner < 0 && MathF.Abs(p.At.X - b.X) < 2f) { _perches[b.PerchIx].Owner = -1; p.Owner = b.Index; b.PerchIx = i; b.Y = p.At.Y; break; }
            }
        b.Mind.Begin(Doing.Perch, Organic.Jitter(Rng, 4f));
        if (b.OnGround) for (int i = 0; i < _seeds.Length; i++)
                if (_seeds[i].Life > 0f && MathF.Abs(_seeds[i].X - b.X) < 0.03f * _u) { b.PeckN = 6; b.Clock = 0; b.Mind.Begin(Doing.Peck, PeckLen(6)); }
    }

    // ---- Squabble: one leans in with the beak open, the other gives way or answers ----
    private void DoSquabble(Bird b, BirdBook bk, float dt)
    {
        b.SquabbleT += dt;
        float t = b.SquabbleT;
        float lunge = t < 0.25f ? 0f : t < 0.5f ? Organic.Smooth((t - 0.25f) / 0.25f) : t < 0.9f ? 1f - Organic.Smooth((t - 0.5f) / 0.4f) : 0f;
        b.X += b.SquabbleDir * 0.0f;
        b.Page = t < 0.2f ? bk.Head[0] : bk.Lean; b.PageName = t < 0.2f ? "stand" : "lean";
        b.Lift = 0f;
        b.X = b.HopFrom + b.SquabbleDir * lunge * 0.10f * bk.K;
    }

    private void BeginSquabble(Bird agg, Bird vic)
    {
        int want = vic.X > agg.X ? 1 : -1;
        if (agg.Dir != want)                                   // a reversal is a TURN: hop round first, then lean in
        {
            agg.Pending = vic; agg.TurnT = 0.30f; agg.TurnFlipped = false; agg.Mind.Begin(Doing.Perch, 0.5f);
            return;
        }
        agg.Mind.Begin(Doing.Squabble, 1.2f); agg.SquabbleT = 0f; agg.SquabbleDir = vic.X > agg.X ? 1 : -1; agg.HopFrom = agg.X;
        Roll(agg, $"squabble with {vic.Index}");
        if (vic.Timid > 0.3f && vic.Kind != BirdKind.Jay) { vic.Plan = 1; vic.AfterFlush = false; BeginStartle(vic, 0.25f); }
        else
        {
            int w2 = agg.X > vic.X ? 1 : -1;
            if (vic.Dir != w2) { vic.TurnT = 0.30f; vic.TurnFlipped = false; vic.Mind.Begin(Doing.Perch, 0.4f); }          // turn to face the aggressor first
            else { vic.Mind.Begin(Doing.Squabble, 1.1f); vic.SquabbleT = 0f; vic.SquabbleDir = w2; vic.HopFrom = vic.X; }
        }
    }

    // ---- Sing: the male cardinal's song, beak open, tiny notes rising ----
    private void DoSing(Bird b, BirdBook bk, float dt)
    {
        float c = (float)(b.Clock % 0.9f);
        if (c < 0.05f && b.NoteT <= 0f) { b.NoteT = 0.5f; Spawn(b.X + b.Dir * 0.2f * bk.K, b.Y - 0.5f * bk.K, 2, 1); }
        b.NoteT -= dt;
        b.Page = c < 0.12f ? bk.Sing[1] : c < 0.26f ? bk.Sing[2] : c < 0.38f ? bk.Sing[1] : c < 0.55f ? bk.Sing[2] : bk.Sing[0];
        b.PageName = "sing";
    }

    // ---- Bathe: wings thrown out, a flurry of powder ----
    private void DoBathe(Bird b, BirdBook bk, float dt)
    {
        int ph = (int)(b.Clock * 14f) % 2;
        b.Page = bk.Bathe[ph]; b.PageName = "bathe" + ph;
        b.NoteT -= dt;
        if (b.NoteT <= 0f) { b.NoteT = 0.05f; Spawn(b.X, b.Y - 0.25f * bk.K, 2, 0); }
    }

    // ---------------------------------------------------------------- deciding what next

    private void Decide(Bird b)
    {
        Span<float> w = stackalloc float[10];
        Spot where = b.OnGround ? Spot.Ground : _perches[b.PerchIx].Kind;
        bool table = where is Spot.Tray or Spot.Ground;
        bool storyHold = b.Story;
        w[0] = 1.3f;                                                                          // stay and look about
        w[1] = table || where == Spot.Roof ? 5f * (0.4f + b.Senses.Hungry) : 0f;              // peck
        w[2] = where is Spot.Tray or Spot.Log or Spot.Ground ? 1.8f : 0f;                     // hop along
        w[3] = b.Kind == BirdKind.Cardinal && where is Spot.Lamp or Spot.Roof or Spot.Branch or Spot.Fir or Spot.Log ? 3.5f : 0f;   // sing
        w[4] = where == Spot.Ground && b.Kind != BirdKind.Jay ? 1.4f : 0f;                    // bathe
        w[5] = b.Kind == BirdKind.Chickadee && where == Spot.Tray ? 1.6f : 0f;                // hang
        w[6] = Lull || storyHold ? 0f : 0.3f;                                                 // move to another perch (a flight is a doing: few of them)
        w[7] = Lull || storyHold ? 0f : 0.2f + 0.012f * b.Stay;                               // leave: a visit lasts a minute or more
        w[8] = where == Spot.Tray && OtherOnTable(b) != null ? 1.6f * (0.3f + b.Bold) * (b.Kind == BirdKind.Jay ? 2f : 1f) : 0f;   // squabble
        w[9] = 1f + 1.5f * b.Lazy;                                                            // fluff up
        if (b.Kind == BirdKind.Jay && where is Spot.Tray or Spot.Roof) { w[7] = Lull ? 0f : (_tableBanned ? 0.1f : 3f); w[6] = 0f; }
        if (storyHold) { b.Mind.Begin(Doing.Perch, 1.5f); return; }
        int pick = Dice.Pick(Rng, w);
        switch (pick)
        {
            case 1: b.PeckN = 7 + Rng.Next(7); b.Clock = 0; b.Flung = false; b.Mind.Begin(Doing.Peck, PeckLen(b.PeckN)); Roll(b, "peck"); break;
            case 2: StartHopGoal(b); Roll(b, "hop"); break;
            case 3: b.Mind.Begin(Doing.Sing, 6f); b.Clock = 0; Roll(b, "sing"); break;
            case 4: b.Mind.Begin(Doing.Bathe, 3.5f); b.Clock = 0; Roll(b, "bathe"); break;
            case 5: b.HangT = b.HangTotal = 1.0f + 0.8f * (float)Rng.NextDouble(); b.Mind.Begin(Doing.Perch, b.HangT + 0.4f); Roll(b, "hang"); break;
            case 6: b.Plan = 1; b.AfterFlush = false; BeginStartle(b, 0.12f); Roll(b, "move"); break;
            case 7: b.Plan = 0; b.AfterFlush = false; BeginStartle(b, 0.12f); Roll(b, "leave"); break;
            case 8: { Bird? o = OtherOnTable(b); if (o != null) BeginSquabble(b, o); else b.Mind.Begin(Doing.Perch, 2f); break; }
            case 9: b.FluffT = Organic.Jitter(Rng, 3.5f); b.Mind.Begin(Doing.Perch, b.FluffT + 0.3f); Roll(b, "fluff"); break;
            default:
                b.Mind.Begin(Doing.Perch, Organic.Jitter(Rng, 9f));
                if (Rng.NextDouble() < 0.18 && !b.OnGround) { b.TurnT = 0.30f; b.TurnFlipped = false; Roll(b, "turn about"); }
                break;
        }
    }

    private Bird? OtherOnTable(Bird b)
    {
        foreach (Bird o in _birds)
            if (o != b && !o.OnGround && o.PerchIx >= 0 && _perches[o.PerchIx].Kind == Spot.Tray && o.Mind.Doing is Doing.Perch or Doing.Peck) return o;
        return null;
    }

    private void StartHopGoal(Bird b)
    {
        if (b.OnGround)
        {
            PointF g = PickGround(b);
            b.GoalX = g.X;
        }
        else
        {
            // the other slot of the same sort of perch if it is free, else a small shuffle where it stands
            Perch me = _perches[b.PerchIx];
            b.GoalX = b.X;
            foreach (Perch p in _perches)
                if (p != me && p.Kind == me.Kind && p.Owner < 0) { b.GoalX = p.At.X; break; }
        }
        b.HopPh = 0f;
        float hopLen = RealLength[(int)b.Kind] * 1.8f * 0.1f * _u * LevelScale[CurrentLevel(b)];
        b.Mind.Begin(Doing.Hop, 6f);
        if (MathF.Abs(b.GoalX - b.X) < 1.2f * hopLen) b.Mind.Begin(Doing.Perch, Organic.Jitter(Rng, 4f));   // nowhere worth a hop: stay put, not a one-frame hop
    }

    // ==================================================================
    //  A STORY'S REQUEST
    // ==================================================================

    /// <summary>A story asks one bird to fly to a point and sit there that long (the feet land ON the point). False if none is free.</summary>
    public bool TryLand(PointF perch, float seconds)
    {
        Bird? pick = null;
        foreach (Bird b in _birds)
        {
            if (b.Story || b.Kind == BirdKind.Jay || b.Mind.Doing is Doing.Startle or Doing.Fly) continue;
            if (b.Mind.Doing == Doing.Away && b.Mind.Left > 0f) { pick = b; break; }
            if (pick == null && b.Visible) pick = b;
        }
        if (pick == null || !Ready) return false;
        var p = new Perch { At = perch, Kind = Spot.Story, FootY = perch.Y, Level = LevelOf(S.FieldScale(perch.Y)), Owner = pick.Index };
        _perches.Add(p);
        pick.Story = true; pick.StoryFor = seconds;
        PointF from = pick.Visible ? new PointF(pick.X, pick.Y) : SourcePoint(pick, perch);
        StartFlight(pick, from, perch, false, _perches.Count - 1, false, pick.Visible ? CurrentLevel(pick) : p.Level, p.Level);
        pick.Story = true;
        return true;
    }

    // ==================================================================
    //  PARTICLES: powder and notes
    // ==================================================================

    private void Spawn(float x, float y, int n, int kind)
    {
        for (int i = 0; i < n; i++)
            for (int j = 0; j < _parts.Length; j++)
                if (!_parts[j].On)
                {
                    float m = 0.1f * _u;
                    float a = (float)Rng.NextDouble() * MathF.PI;
                    _parts[j] = kind == 0
                        ? new Particle { On = true, X = x, Y = y, Vx = MathF.Cos(a) * 0.55f * m * ((float)Rng.NextDouble() + 0.3f), Vy = -MathF.Sin(a) * 0.9f * m, T = 0f, Life = 0.5f + 0.4f * (float)Rng.NextDouble(), Size = (float)Rng.NextDouble(), Kind = 0 }
                        : new Particle { On = true, X = x + i * 0.2f * m, Y = y, Vx = 0.25f * m, Vy = -0.5f * m, T = 0f, Life = 1.3f, Size = 0f, Kind = 1 };
                    break;
                }
    }

    private void TickParticles(float dt)
    {
        for (int i = 0; i < _parts.Length; i++)
        {
            if (!_parts[i].On) continue;
            ref Particle p = ref _parts[i];
            p.T += dt;
            if (p.T >= p.Life) { p.On = false; continue; }
            p.X += p.Vx * dt; p.Y += p.Vy * dt;
            p.Vy += (p.Kind == 0 ? 3.0f * 0.1f * _u : 0f) * dt;       // powder falls back; a note drifts up
        }
    }

    // ==================================================================
    //  DRAWING
    // ==================================================================

    public override void Collect(DepthQueue q)
    {
        if (_birds.Length == 0) return;
        foreach (Bird b in _birds)
        {
            if (!b.Visible || b.Page == null) continue;
            float key = b.Mind.Doing == Doing.Fly ? 100000f + b.Y : b.OnGround ? b.Y : b.PerchIx >= 0 && _perches[b.PerchIx].Kind != Spot.Branch ? _perches[b.PerchIx].FootY + 1f : 100000f + b.Y;
            q.Add(key, this, b.Index);
        }
        q.Add(200000f, this, 1000);
    }

    public override void DrawOne(FrameBuffer fb, int index)
    {
        if (index == 1000) { DrawParticles(fb); return; }
        Bird b = _birds[index];
        if (b.Page == null || b.Book == null) return;
        BirdBook bk = b.Book;
        bool mirror = b.Dir < 0;
        if (b.Ground && b.Mind.Doing != Doing.Fly)
            Stamp(fb, bk.Shadow, b.X, b.Y + 1f, bk.ShadowOx, bk.ShadowOy, false, 1f);
        // At a FRACTION of a pixel (Sprite.DrawSmooth): a flying or hopping bird glides instead of stepping a whole pixel at a time.
        float lx = b.X - (mirror ? b.Page.Width - 1 - bk.Ox : bk.Ox), ty = b.Y - b.Lift - bk.Oy;
        bool flying = b.Mind.Doing == Doing.Fly;
        int near = flying ? 255 : b.Near;
        bool pine = flying && b.Pine >= 0 && _mask != null;
        if (b.Prev != null && b.Fade < 1f)
        {
            float px = b.X - (mirror ? b.Prev.Width - 1 - bk.Ox : bk.Ox);
            if (pine) DrawBehindPine(fb, b.Prev, px, ty, mirror, 1f, near, b.Pine); else b.Prev.DrawSmooth(fb, px, ty, 1f, null, mirror, S.NearDepth, near);
            if (pine) DrawBehindPine(fb, b.Page, lx, ty, mirror, b.Fade, near, b.Pine); else b.Page.DrawSmooth(fb, lx, ty, b.Fade, null, mirror, S.NearDepth, near);
        }
        else if (pine) DrawBehindPine(fb, b.Page, lx, ty, mirror, 1f, near, b.Pine);
        else b.Page.DrawSmooth(fb, lx, ty, 1f, null, mirror, S.NearDepth, near);
    }

    /// <summary>
    /// A bird going into or coming out of a pine passes BEHIND its outline: no see-through fade (the review: birds faded
    /// in and out in mid-air at the pines). The stencil is the whole screen "true" except, for this one stamp, the inside of
    /// the pine's cone (the same cone the painter drew: Brushwork.PineHalfWidth at each row) under the bird's rectangle.
    /// Where the stencil is false the stamp leaves the frame alone, so the pine hides the bird. The cone is put back after.
    /// </summary>
    private void DrawBehindPine(FrameBuffer fb, Sprite page, float lx, float ty, bool mirror, float alpha, int near, int pineIx)
    {
        var pine = S.Pines[pineIx];
        int x0 = Math.Max(0, (int)MathF.Floor(lx)), x1 = Math.Min(S.Width, (int)MathF.Ceiling(lx) + page.Width + 1), y0 = Math.Max(0, (int)MathF.Floor(ty)), y1 = Math.Min(S.Height, (int)MathF.Ceiling(ty) + page.Height + 1);
        for (int pass = 0; pass < 2; pass++)
        {
            for (int y = y0; y < y1; y++)
            {
                if (y < pine.Top || y > pine.Foot) continue;
                float hw = 0.92f * Brushwork.PineHalfWidth(y, pine.Top, pine.Foot, _u);
                int a = Math.Max(x0, (int)MathF.Ceiling(pine.X - hw)), c = Math.Min(x1, (int)MathF.Floor(pine.X + hw) + 1);
                for (int x = a; x < c; x++) _mask![y * S.Width + x] = pass == 1;
            }
            if (pass == 0) page.DrawSmooth(fb, lx, ty, alpha, _mask, mirror, S.NearDepth, near);
        }
    }

    private void DrawParticles(FrameBuffer fb)
    {
        for (int i = 0; i < _parts.Length; i++)
        {
            if (!_parts[i].On) continue;
            Particle p = _parts[i];
            float k = 1f - p.T / p.Life;
            if (p.Kind == 1) _noteSprite.DrawCentered(fb, p.X, p.Y, 0.8f * MathF.Min(1f, k * 2f));
            else _dot[p.Size < 0.3f ? 1 : 2].DrawCentered(fb, p.X, p.Y, 0.9f * k);
        }
        for (int i = 0; i < _seeds.Length; i++)
            if (_seeds[i].Life > 0f) _seedSprite.DrawCentered(fb, _seeds[i].X, _seeds[i].Y, MathF.Min(1f, _seeds[i].Life / 3f));
        if (_flung.On) _seedSprite.DrawCentered(fb, _flung.X, _flung.Y);
    }
}
