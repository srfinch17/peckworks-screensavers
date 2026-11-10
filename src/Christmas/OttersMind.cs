using Peckworks.Screensavers.Core;
using Peckworks.Screensavers.Core.Minds;

namespace Christmas;

// ============================================================================
// THE OTTERS' MINDS (the animal and its body are in OttersArt.cs and Otters.cs).
//
// FEYNMAN VERSION. Each otter is a small mind with one thing it is doing at a
// time (an enum), a few personality dials dealt at birth (how playful, how
// lazy, how timid, how curious), and eyes and ears on the scene's noticeboard.
//
//   THE WORLD. The pond is an ellipse of ice lying flat on the snow. A thing
//   walking on it moves in the FLAT plane (x across, "z" toward us), and the
//   picture squashes z by Ry/Rx (the ellipse's own squash), so the otter's
//   heading is an angle in the flat plane and its screen position is
//   (x, cy + z * squash). That is also why the otter's picture is painted at
//   many YAWS: when its heading is toward us it must really face us.
//
//   THE DOOR. The ice hole is open dark water, the otters' door. An otter
//   that is "Under" is in the water, unseen (a thin line of bubbles under the
//   ice now and then gives it away). It surfaces in the hole (head up, ripples),
//   hauls out over the rim, or dives back in. Hauling out and diving are drawn
//   in the hole's own place: their page sets keep their origin at the spot
//   where the otter stands, so the otter must stand exactly there first.
//
//   EVERY MOVE ON LAND IS A PACE (Core/Minds/Mover.cs): the gallop leans in,
//   and brakes onto the page where both feet are down; the gallop's page
//   follows the DISTANCE walked, never the clock, so a planted foot stays
//   planted. A gallop to a goal measures the straight distance first and
//   fits the stride to it (a whole number of strides, each within a few
//   percent of an otter's real one), so the last stride ends exactly on the
//   goal with the feet down.
//
//   A TURN IS A TURN. Heading comes round at a limited rate and the page is
//   chosen from the nine yaws, so an otter that wants to go the other way
//   pivots through facing us (or facing away), never flipping in one frame.
//
// DOINGS, WHAT CAN CUT EACH SHORT AND THE STATE EACH EXIT LEAVES:
//   Under     in the water, unseen. Ends by its timer (an alarm stretches it). Leaves: at the hole, no fish
//             unless WantFish (then it surfaces with a fish).
//   Surface   head up in the hole. Ends by its timer, or a Threat/Loud (it sinks at once). Leaves: Under, or
//             swimming to the rim to Haul out (head at the haul's first page).
//   Haul      hauling out over the rim. Not interrupted (a second and a half). Leaves: standing at rest on the
//             ice (or sitting to eat, with a fish).
//   Rest      standing idle, the head turning. Ends by its timer, a reaction or the dice. Leaves: standing.
//   Gallop    the otter's looping run to a goal; a Next says what happens on arrival. A Threat/Loud
//             turns it into a panicked gallop for the hole. Leaves: standing, feet down.
//   Slide     a run-up of three or four bounds, a belly flop, a long glide, a scramble up. Cut short only
//             before the flop. Leaves: standing at rest.
//   Stand     upright like a periscope, looking at something. Ends by its timer. Leaves: standing at rest.
//   Roll      on its back in the snow at the rim, paws waving. Not interrupted. Leaves: standing at rest.
//   Eat       sits up and eats a fish held in both paws; the other otter comes and begs. Leaves: standing.
//   Dive      the leap and plunge into the hole. Not interrupted. Leaves: Under.
//   Wrestle   two otters in one rolling ball (the partner is the same doing). Leaves: both sliding apart.
//
// WHAT THEY PIN ON THE BOARD: Movement at an otter that starts a belly slide (reach 0.3 U, weight 0.4, once per
// slide: a slide takes seconds, so never a flood); Friend when one bolts for the hole (reach 0.5 U, weight 0.8,
// at most once in 15 s); Food at an otter that starts eating a fish (reach 0.2 U, weight 0.35, once per fish).
// Who is in reach: the Movement (0.3 U = about 320 px at 1080p) reaches the mice that come to the pond's near
// rim and a hare at the strip left of the pond; the Friend reaches no one else but the other otter; the Food
// reaches the mice at the rim (they may come and look).
// ============================================================================
internal sealed partial class Otters
{
    public int Count => _o.Length;
    public PointF OtterAt(int i) => i >= 0 && i < _o.Length ? new PointF(_o[i].X, _o[i].Y) : PointF.Empty;
    /// <summary>True when no otter is on the ice or in the hole: every one is under the water.</summary>
    public bool AllUnder
    {
        get { foreach (Otter o in _o) if (o.M.Doing != Doing.Under) return false; return true; }
    }

    private enum Doing { Under, Surface, Haul, Rest, Gallop, Slide, Stand, Roll, Eat, Dive, Wrestle }
    private enum After { Rest, Dive, DiveFish, Roll, Beg, Wrestle, Slide, Tobog }

    private sealed class Otter
    {
        public int I;
        public float X, Y, Hd;                              // feet (screen), heading in the flat plane (radians; 0 = right, +pi/2 = toward us)
        public readonly Mind<Doing> M = new(Doing.Under);
        public Pace Pace = new(1f);
        public Attention Att = new(0.4f, 0.5f);
        public float Playful, Lazy;
        public double T;                                    // seconds in the doing
        public int Stage;
        public After Next;
        public float Speed;                                 // slide speed, pixels a second in the flat plane
        public float GoalX, GoalY, GoalHd, Stride;
        public bool HaveGoalHd, Panic, Braked, Chasing, Fish, WantFish;
        public int Face = 1;
        public int HeadIdx = 2, HeadTarget = 2, Level;
        public float HeadClock, LookClock, Alarm, RunN, OutTime, BubClock, BubDir, BubLen, BubS;
        public float LookX, LookY; public bool HasLook;
        public int Partner = -1, Slides, Bite;
        public readonly Cooldown PostRest = new(), StandCool = new();
        public float BallTh, BallVx, BallVy, Chord;
        public string PageKind = ""; public int PageIdx; public float ShakeT;
        public float Spin;                                  // slide's turn rate (rad/s)
        public float BlinkT, EarT, WhiskT, CurlT, ScratchT, NextBlink = 1.5f, NextEar = 5f, NextWhisk = 3f, NextCurl = 8f, NextScratch = 12f, NextBreath = 2f;
        public float Blocked;                               // seconds a gallop has been refused its steps (the run ends after 0.7 s of it)
        public int Var = -1;                                // an idle fidget on the Rest page: 0 blink, 1 ear flick, 2 whiskers, 3 tail curl
        public float BX, BY, TX, TY, TobogHd, Lift, LiftMax; // the snow slide: the foot of the drift, its top, the way down, how high the feet are lifted (metres)
        public float BallX, BallY; public int BallTurns;
        public float WayX, WayY; public bool HasWay, PassedWay;   // a waypoint round the hole when the straight way crosses it
        // what to draw this frame
        public bool Vis; public Sprite? Spr; public int OX, OY; public bool Mirror; public float FX, FY, ShakeDX; public int ShadowBin = -1;
    }

    private Otter[] _o = [];
    /// <summary>The snow drifts just outside the near rim an otter can climb: the foot of each (on the ice), its top (on the snow), and the angle round the pond. Found from the painted pixels (white snow), as the painter does not publish them.</summary>
    private readonly List<(float Bx, float By, float Tx, float Ty, float Th)> _banks = new();
    private float _p, _stride, _cx, _cy, _rx, _ry, _r, _hx, _hy, _hrx, _hry;
    private double _time;
    private string? _force; private bool _forced;
    private StreamWriter? _log;
    private float _fishClock = 0f;

    // ---- loose bits that make the act: ripples, droplets, puffs, bubbles, ice scratches ----
    private struct Ring { public float X, Y, Age, Max, MaxR; }
    private struct Bit { public float X, Y, H, VX, VY, VH, Age, Max; public int Kind; }       // Kind 0 droplet, 1 puff
    private struct Bub { public float X, Y, Age; }
    private struct Scr { public float X, Y, Age; public bool Joined; }
    private readonly Ring[] _rings = new Ring[24];
    private readonly Bit[] _bits = new Bit[220];
    private readonly Bub[] _bubs = new Bub[40];
    private readonly Scr[] _scr = new Scr[260];
    private int _ringN, _bitN, _bubN, _scrN;
    private readonly Random _fx = new(11);                  // the little bits' own dice: cosmetic only, never steers anything

    private void InitMind()
    {
        _p = Math.Max(4f, S.Metre(S.Pond.At.Y));
        _stride = 0.80f * _p;
        _cx = S.Pond.At.X; _cy = S.Pond.At.Y; _rx = S.Pond.Rx; _ry = S.Pond.Ry; _r = _ry / _rx;
        _hx = S.IceHole.At.X; _hy = S.IceHole.At.Y; _hrx = S.IceHole.Rx; _hry = S.IceHole.Ry;
        _force = Environment.GetEnvironmentVariable("PECKWORKS_OTTER");
        _o = new Otter[_count];
        float[] timid = Traits.Deal(Rng, _count), curious = Traits.Deal(Rng, _count), playful = Traits.Deal(Rng, _count);
        for (int i = 0; i < _count; i++)
        {
            var o = new Otter { I = i, Playful = playful[i], Lazy = (float)Rng.NextDouble() };
            o.Att = new Attention(0.25f + 0.5f * timid[i], 0.35f + 0.65f * curious[i]) { Keen = 1.1f, Hungry = 0.55f + 0.45f * (float)Rng.NextDouble() };
            o.Pace = new Pace(4.5f * _p);
            // The first starts standing on the ice, the rest in the water, one by one: it is a pond, and they are not all out at once.
            // THE ENTRANCE (the brief): nobody stands there at the start. Every otter is under the ice and its head comes up in
            // the hole, one after the other: the first about 1.5 s in, the next three or four seconds later each.
            o.M.Begin(Doing.Under, 1.5f + 3.6f * i + (float)Rng.NextDouble() * 1.5f); o.Vis = false;
            _o[i] = o;
        }
        for (int i = 0; i < _count; i++) _o[i].I = i;
        if (_force != null && _count > 0)                   // a test run: everyone out on the ice, ready for the forced doing at 6 s
            for (int i = 0; i < _count; i++) { if (_o[i].M.Doing == Doing.Under) { StartStanding(_o[i]); _o[i].M.Begin(Doing.Rest, 30f); } }
        FindBanks();
        string? lp = Environment.GetEnvironmentVariable("PECKWORKS_MINDLOG");
        if (!string.IsNullOrEmpty(lp)) _log = MindLog.Open();
        if (_log != null) Log($"banks found: {_banks.Count}");
    }

    private void FindBanks()
    {
        uint[] px = S.Pixels; int W = S.Width, H = S.Height;
        bool White(float x, float y)
        {
            int ix = (int)x, iy = (int)y;
            if (ix < 0 || iy < 0 || ix >= W || iy >= H) return false;
            uint c = px[iy * W + ix];
            return ((c >> 16) & 255) >= 222 && ((c >> 8) & 255) >= 232 && (c & 255) >= 246;
        }
        float lastTh = -9f;
        for (int k = 0; k < 60; k++)
        {
            float th = (0.06f + 0.88f * k / 59f) * MathF.PI, cs = MathF.Cos(th), sn = MathF.Sin(th);
            if (!(White(_cx + cs * _rx * 1.03f, _cy + sn * _ry * 1.03f) && White(_cx + cs * _rx * 1.06f, _cy + sn * _ry * 1.06f) && White(_cx + cs * _rx * 1.09f, _cy + sn * _ry * 1.09f))) continue;
            if (th - lastTh < 0.16f * MathF.PI) continue;
            lastTh = th;
            _banks.Add((_cx + cs * _rx * 0.90f, _cy + sn * _ry * 0.90f, _cx + cs * _rx * 1.07f, _cy + sn * _ry * 1.07f, th));
        }
    }

    private void StartStanding(Otter o)
    {
        // A spot on the ice in the pond's left half, away from the hole.
        for (int tries = 0; tries < 30; tries++)
        {
            float a = (float)Rng.NextDouble() * MathF.Tau, d = 0.15f + 0.5f * (float)Rng.NextDouble();
            float x = _cx + MathF.Cos(a) * _rx * d - 0.15f * _rx, y = _cy + MathF.Sin(a) * _ry * d;
            bool apart = true;
            for (int j = 0; j < o.I; j++) if (PlaneDist(x, y, _o[j].X, _o[j].Y) < 1.6f * _p) apart = false;
            if (Allowed(x, y) && apart) { o.X = x; o.Y = y; break; }
        }
        if (o.X == 0) { o.X = _cx - _rx * 0.3f; o.Y = _cy; }
        o.Hd = (float)Rng.NextDouble() * MathF.Tau - MathF.PI;
        o.M.Begin(Doing.Rest, 2f + 3f * (float)Rng.NextDouble());
    }

    // ---------------------------------------------------------------- the flat world

    private bool InHole(float x, float y, float grow)
    {
        float dx = (x - _hx) / (_hrx * grow), dy = (y - _hy) / (_hry * grow);
        return dx * dx + dy * dy < 1f;
    }
    /// <summary>How far from the hole's middle, in hole-radii (1 = on its rim).</summary>
    private float HoleRadius(float x, float y) { float dx = (x - _hx) / _hrx, dy = (y - _hy) / _hry; return MathF.Sqrt(dx * dx + dy * dy); }
    private bool Allowed(float x, float y) => S.OnPond(x, y, 1.10f) && !InHole(x, y, 1.22f) && PropFree(x, y, 0.45f * _p);
    /// <summary>Clear of the tiny fir that stands at the pond's right end (the one prop near the ice): nothing of an otter goes through its branches.</summary>
    private bool PropFree(float x, float y, float room) => !(MathF.Abs(x - S.SaplingFoot.X) < S.SaplingHeight * 0.30f + room && MathF.Abs(y - S.SaplingFoot.Y) < 0.05f * S.U);
    private float HeadingTo(Otter o, float tx, float ty) => MathF.Atan2((ty - o.Y) / _r, tx - o.X);
    private float PlaneDist(float ax, float ay, float bx, float by) { float dx = bx - ax, dz = (by - ay) / _r; return MathF.Sqrt(dx * dx + dz * dz); }

    /// <summary>Moves "d" pixels along the heading in the flat plane. False (and no move) if the step would leave the ice or enter the hole.</summary>
    private bool Move(Otter o, float d)
    {
        if (d <= 0.001f) return true;                       // a standing start (the pace has not picked up yet): no step to refuse
        float nx = o.X + MathF.Cos(o.Hd) * d, ny = o.Y + MathF.Sin(o.Hd) * d * _r;
        if (!Allowed(nx, ny))
        {
            // A WAY OUT OF THE HOLE'S MARGIN (found in the log: an otter standing at 20 px from the hole's middle, inside the
            // keep-out ring, refused EVERY step, so each gallop it was given ended where it began, 1.6 lengths short, and it
            // rested two seconds and rolled the same impossible gallop again, a dozen times a minute). If the otter already
            // stands where it should not, a step that takes it AWAY from the hole's middle and stays on the ice is allowed.
            if (Allowed(o.X, o.Y) || !S.OnPond(nx, ny, 1.10f) || HoleRadius(nx, ny) <= HoleRadius(o.X, o.Y)) return false;
        }
        o.X = nx; o.Y = ny; return true;
    }

    /// <summary>The way out of the pond or round the hole: tries the wanted heading, then 30 and 60 degrees either way, probing half a metre ahead.</summary>
    private float Clear(Otter o, float want)
    {
        float probe = 0.55f * _p;
        for (int k = 0; k < 5; k++)
        {
            float off = k == 0 ? 0f : (k % 2 == 1 ? 1f : -1f) * ((k + 1) / 2) * 0.52f;
            float h = want + off;
            if (Allowed(o.X + MathF.Cos(h) * probe, o.Y + MathF.Sin(h) * probe * _r)) return h;
        }
        return want + 1.6f;
    }

    private void ComputeYaw(float hd, out int bin, out bool mirror)
    {
        float deg = hd * 180f / MathF.PI;
        deg = MathF.IEEERemainder(deg, 360f);
        if (MathF.Cos(hd) >= 0f) { bin = OttersArt.BinOf(deg); mirror = false; }
        else { bin = OttersArt.BinOf(deg > 0 ? 180f - deg : -180f - deg); mirror = true; }
    }

    private void Begin(Otter o, Doing d, float secs = Mind<Doing>.Forever) { o.M.Begin(d, secs); o.T = 0; o.Stage = 0; }

    // ---------------------------------------------------------------- the hole's two spots

    /// <summary>Where an otter stands to leap into the hole from the side "face" (+1: from the left, facing right), and where it enters.</summary>
    private void DiveSpot(int face, out float x, out float y, out float edge)
    {
        edge = _hx - face * _hrx * 0.88f;
        x = edge - face * 0.83f * _p;
        y = _hy + 0.25f * _hry;
    }
    private bool SpotOk(float x, float y) => S.OnPond(x, y, 1.06f) && !InHole(x, y, 1.12f);
    /// <summary>Where an otter ends up standing after hauling out to the side "face" (+1: out over the right rim of the hole).</summary>
    private void HaulSpot(int face, out float x, out float y, out float edge)
    {
        edge = _hx + face * _hrx * 0.9f;
        x = edge + face * 0.66f * _p;
        y = _hy + 0.25f * _hry;
    }

    // ---------------------------------------------------------------- the update

    public override void Update(float dt)
    {
        if (_o.Length == 0 || _pg == null) return;
        _time += dt;
        if (_force != null && !_forced && _time >= 6.0) { _forced = true; Force(); }
        _fishClock += dt;
        foreach (Otter o in _o)
        {
            o.T += dt; o.Att.Tick(dt); o.M.Tick(dt); o.PostRest.Tick(dt); o.StandCool.Tick(dt);
            if (o.M.Doing != Doing.Under) o.OutTime += dt; else o.OutTime = 0f;
            if (o.Alarm > 0) o.Alarm -= dt;
            React(o);
            switch (o.M.Doing)
            {
                case Doing.Under: UpdateUnder(o, dt); break;
                case Doing.Surface: UpdateSurface(o, dt); break;
                case Doing.Haul: UpdateHaul(o, dt); break;
                case Doing.Rest: UpdateRest(o, dt); break;
                case Doing.Gallop: UpdateGallop(o, dt); break;
                case Doing.Slide: UpdateSlide(o, dt); break;
                case Doing.Stand: UpdateStand(o, dt); break;
                case Doing.Roll: UpdateRoll(o, dt); break;
                case Doing.Eat: UpdateEat(o, dt); break;
                case Doing.Dive: UpdateDive(o, dt); break;
                case Doing.Wrestle: UpdateWrestle(o, dt); break;
            }
            if (OnGround(o)) S.Occupy(this, o.I, o.X, o.Y, 0.5f * _p);
            Present(o);
        }
        Separate(dt);
        UpdateBits(dt);
        if (_log != null) foreach (Otter o in _o)
        {
            // The place that is DRAWN: while two otters are one rolling ball, each is drawn half a pair-gap to its side of the ball's middle.
            float lx = o.X;
            if (o.M.Doing == Doing.Wrestle && o.Stage is >= 1 and <= 3 && o.I < o.Partner) lx = o.X + (o.GoalX - o.BallX);
            _log.WriteLine($"{Board.Time:F2} Otter {o.I} {lx:F1} {o.Y:F1} {(o.M.Doing == Doing.Gallop ? o.Pace.Speed : o.Speed):F0} {o.M.Doing}/{o.Stage} {o.PageKind}{o.PageIdx} {o.Hd:F3} {(o.Vis ? 1 : 0)}");
        }
    }

    private void Log(string s) { _log?.WriteLine($"{Board.Time:F2} Otter {s}"); }

    // ---------------------------------------------------------------- reactions

    private void React(Otter o)
    {
        Doing d = o.M.Doing;
        if (d == Doing.Under || d == Doing.Haul || d == Doing.Dive || d == Doing.Wrestle) return;
        Reaction r = o.Att.Check(Board, new PointF(o.X, o.Y), o, out Notice n);
        if (r == Reaction.None) return;
        if (_log != null) Log($"{o.I} reaction {r} {n.Kind}");
        if ((n.Kind == Sense.Threat || n.Kind == Sense.Loud) && (r == Reaction.Flee || r == Reaction.Freeze))
        {
            foreach (Otter q in _o) Alarmed(q, q == o ? 0f : 0.25f * q.I);
            if (o.PostRest.Ready) { o.PostRest.Start(15f); Board.Post(Sense.Friend, new PointF(o.X, o.Y), 0.5f * S.U, 0.8f, o); }
        }
        else if (r == Reaction.Approach && n.Kind == Sense.Food)
        {
            if ((d == Doing.Rest || d == Doing.Stand) && !o.Fish && OtherEating(o, out Otter eater))
            {
                o.GoalX = eater.X - (eater.Hd > -MathF.PI / 2 && eater.Hd < MathF.PI / 2 ? 1 : -1) * 0.55f * _p; o.GoalY = eater.Y + 0.1f * _p;
                if (Allowed(o.GoalX, o.GoalY) && PlaneDist(o.X, o.Y, o.GoalX, o.GoalY) > 0.5f * _p) StartGallop(o, o.GoalX, o.GoalY, After.Beg, false, eater.Hd);
                else Look(o, eater.X, eater.Y);
            }
        }
        else if (r == Reaction.Look || r == Reaction.Freeze)
            Look(o, n.At.X, n.At.Y);
    }

    private bool OtherEating(Otter me, out Otter eater)
    {
        foreach (Otter q in _o) if (q != me && q.M.Doing == Doing.Eat) { eater = q; return true; }
        eater = me; return false;
    }

    /// <summary>Look at a place: standing up (or turning the head, if in the hole) toward it.</summary>
    private void Look(Otter o, float x, float y)
    {
        o.LookX = x; o.LookY = y; o.HasLook = true; o.LookClock = 0f;
        Doing d = o.M.Doing;
        if (d == Doing.Rest && !o.Fish && o.StandCool.Ready && Rng.NextDouble() < 0.5) { o.StandCool.Start(35f); StartStand(o, x, y); }
        else if (d == Doing.Stand || d == Doing.Surface) { /* already looking: Update steers to o.Look */ }
    }

    /// <summary>The alarm: every otter on the ice gallops for the hole; one in the hole sinks; one under stays under longer, and heads come up one at a time.</summary>
    private void Alarmed(Otter o, float delay)
    {
        o.Alarm = 9f;
        switch (o.M.Doing)
        {
            case Doing.Under: o.M.Left = MathF.Max(o.M.Left, 3.5f + 2.2f * o.I + delay); break;
            case Doing.Surface: o.Stage = 3; o.T = 0; break;                                    // sinks now
            case Doing.Slide: if (o.Stage < 3) GoDive(o, true, false); break;
            case Doing.Rest: case Doing.Stand: case Doing.Roll: case Doing.Gallop: case Doing.Eat:
                if (o.M.Doing == Doing.Gallop && o.Panic) break;
                GoDive(o, true, false); break;
        }
    }

    // ---------------------------------------------------------------- UNDER and SURFACE

    private void UpdateUnder(Otter o, float dt)
    {
        o.Vis = false; o.Speed = 0f;
        // A line of bubbles under the ice, out from the hole and back, gives the swimmer away now and then.
        if (o.T > 1.2 && o.BubLen > 0f)
        {
            o.BubClock += dt;
            if (o.BubClock > 0.26f)
            {
                o.BubClock = 0f; o.BubS += 0.26f * 0.8f * _p;
                float s = o.BubS < o.BubLen ? o.BubS : 2f * o.BubLen - o.BubS;
                if (s > 0f)
                {
                    float x = _hx + MathF.Cos(o.BubDir) * s, y = _hy + MathF.Sin(o.BubDir) * s * _r;
                    if (S.OnPond(x, y, 0.95f)) AddBubble(x, y);
                }
                if (o.BubS > 2f * o.BubLen) o.BubLen = 0f;
            }
        }
        if (!o.M.Over) return;
        // Up again. Often to look about, sometimes with a fish, sometimes straight out to haul onto the ice.
        o.X = _hx; o.Y = _hy;
        Begin(o, Doing.Surface, Organic.Jitter(Rng, 3.2f, 0.7f) + 1.2f);
        o.Hd = Rng.Next(2) == 0 ? 0f : MathF.PI;
        o.Fish = o.WantFish; o.WantFish = false;
        if (o.Alarm > 0f) o.M.Left = 2.5f + 1.5f * (float)Rng.NextDouble();               // a nervous peek, then down
        AddRing(_hx, _hy, 1.2f);
        if (_log != null) Log($"{o.I} surfaces fish={o.Fish}");
    }

    private void UpdateSurface(Otter o, float dt)
    {
        const float rise = 0.7f, sink = 0.6f;
        o.Spin = 0f;
        // Which way the head faces: it turns about slowly, and follows a thing it has noticed.
        o.LookClock += dt;
        if (o.Stage == 1 && o.T > rise)
        {
            if (o.HasLook && o.LookClock < 4f) o.GoalHd = LookHeading(o, 0f);
            else if (o.LookClock > 0f && (o.HeadClock -= dt) <= 0f)
            {
                o.HeadClock = Organic.Jitter(Rng, 3.8f, 0.6f) + 1.2f;
                o.GoalHd = o.Hd + (float)(Rng.NextDouble() - 0.5) * 2.6f;                      // a new look, a part of the way round (never a whirl)
                if (Rng.Next(3) == 0) AddRing(_hx, _hy, 0.5f);
            }
            o.Hd = Turn.Toward(o.Hd, o.GoalHd, 1.8f, dt);
        }
        if (o.Stage == 0) { if (o.T >= 0.01) o.Stage = 1; }
        if (o.Stage == 1 && o.M.Over)
        {
            if (o.Fish) { StartHaul(o, true); return; }
            if (o.Alarm <= 0f && !Lull && o.OutTime > 0 && Rng.NextDouble() < 0.42) { StartHaul(o, false); return; }
            o.Stage = 3; o.T = 0;
        }
        if (o.Stage == 3 && o.T > sink)                                                        // down again
        {
            Begin(o, Doing.Under, Math.Clamp(Organic.Jitter(Rng, 9f, 0.6f), 5f, 30f));
            o.Vis = false; o.BubDir = (float)(Rng.NextDouble() * MathF.Tau); o.BubLen = Rng.NextDouble() < 0.6 ? (1.0f + 1.6f * (float)Rng.NextDouble()) * _p : 0f; o.BubS = 0f;
            if (o.Alarm > 0f) o.M.Left += 2f + 2.2f * o.I;
            if (_log != null) Log($"{o.I} dives under {o.M.Left:F1}s");
            return;
        }
        if (o.Stage == 4) SwimToHaul(o, dt);
    }

    /// <summary>The heading toward the thing it is watching (for an overhead thing, simply the side it is on).</summary>
    private float LookHeading(Otter o, float flat)
    {
        float dx = o.LookX - o.X, dz = (o.LookY - o.Y) / _r;
        if (o.LookY < o.Y - 3f * _ry) return dx >= 0 ? 0f : MathF.PI;                          // something high up: face its side
        return MathF.Atan2(dz, dx);
    }

    private void StartHaul(Otter o, bool fish)
    {
        // Pick the side with room, the nearer first.
        int face = Rng.Next(2) == 0 ? 1 : -1;
        HaulSpot(face, out float x, out float y, out _);
        if (!SpotOk(x, y)) { face = -face; HaulSpot(face, out x, out y, out _); }
        o.Face = face; o.Fish = fish;
        o.GoalX = x - face * 0.73f * _p; o.GoalY = y;                                           // where the head will be at the haul's first page
        o.Stage = 4; o.T = 0; o.Speed = 0f;
    }

    private void SwimToHaul(Otter o, float dt)
    {
        // The head swims across the water to the rim, turning to face the way it will climb out.
        o.Hd = Turn.Toward(o.Hd, o.Face > 0 ? 0f : MathF.PI, 5f, dt);
        float dx = o.GoalX - o.X, dy = o.GoalY - o.Y, d = MathF.Sqrt(dx * dx + dy * dy);
        float step = 0.6f * _p * dt;
        if (d <= step + 0.5f)
        {
            o.X = o.GoalX; o.Y = o.GoalY;
            Begin(o, Doing.Haul, 1.7f);
            HaulSpot(o.Face, out float hx, out float hy, out _);
            o.X = hx; o.Y = hy; o.Hd = o.Face > 0 ? 0f : MathF.PI;
            if (_log != null) Log($"{o.I} hauls out face={o.Face} fish={o.Fish}");
            return;
        }
        o.X += dx / d * step; o.Y += dy / d * step;
        o.BubClock += dt;
        if (o.BubClock > 0.22f) { o.BubClock = 0f; AddRing(o.X, o.Y, 0.18f); }
    }

    // ---------------------------------------------------------------- HAUL and DIVE

    private void UpdateHaul(Otter o, float dt)
    {
        const float len = 1.7f;
        o.Speed = 0f;
        if (o.T < 1.0 && _fx.NextDouble() < dt * 22) Drip(o.X - o.Face * 0.5f * _p, o.Y, 1.2f);
        if (o.T >= len)
        {
            o.Hd = o.Face > 0 ? 0f : MathF.PI;
            if (o.Fish) { Begin(o, Doing.Eat, 12f); o.Bite = 0; if (_log != null) Log($"{o.I} eats a fish"); if (_fishClock > 0) _fishClock = 0; Board.Post(Sense.Food, new PointF(o.X, o.Y), 0.3f * S.U, 0.8f, o); }
            else { Begin(o, Doing.Rest, 2.5f + 3f * (float)Rng.NextDouble()); o.ShakeT = 0.9f; }
        }
    }

    private void UpdateDive(Otter o, float dt)
    {
        const float len = 1.35f;
        o.Speed = 0f;
        if (o.Stage == 0 && o.T > 0.62)
        {
            o.Stage = 1;                                                                        // the plunge: splash and ripples where the head went in
            float ex = o.X + o.Face * 0.83f * _p;
            Splash(ex, _hy + 0.1f * _hry);
            AddRing(_hx, _hy, 1.5f);
        }
        if (o.T >= len)
        {
            Begin(o, Doing.Under, Math.Clamp(Organic.Jitter(Rng, 10f, 0.6f), 8f, 30f));
            o.Vis = false; o.X = _hx; o.Y = _hy;
            o.BubDir = (float)(Rng.NextDouble() * MathF.Tau); o.BubLen = Rng.NextDouble() < 0.6 ? (1.0f + 1.6f * (float)Rng.NextDouble()) * _p : 0f; o.BubS = 0f;
            if (o.Alarm > 0f) o.M.Left = 3.5f + 2.2f * o.I + 2f * (float)Rng.NextDouble();
            if (_log != null) Log($"{o.I} under {o.M.Left:F1}s");
        }
    }

    private void GoDive(Otter o, bool panic, bool fish)
    {
        int best = 0; float bestD = float.MaxValue; float bx = 0, by = 0;
        for (int face = -1; face <= 1; face += 2)
        {
            DiveSpot(face, out float x, out float y, out _);
            if (!SpotOk(x, y)) continue;
            float d = PlaneDist(o.X, o.Y, x, y);
            if (d < bestD) { bestD = d; best = face; bx = x; by = y; }
        }
        if (best == 0) { Begin(o, Doing.Rest, 3f); return; }
        o.Face = best; o.WantFish = fish;
        StartGallop(o, bx, by, fish ? After.DiveFish : After.Dive, panic, best > 0 ? 0f : MathF.PI);
    }

    // ---------------------------------------------------------------- REST

    private void UpdateRest(Otter o, float dt)
    {
        o.Speed = 0f; o.Pace.Halt();
        // Idle business: the head turns in steps through the pages, now to one side, now the other; the whole body
        // turns slowly on the spot to look at the other otter or at whatever it has noticed.
        o.HeadClock -= dt;
        if (o.HeadClock <= 0f)
        {
            // CALM HEAD (the review: it re-aimed every 1.5 s): a held look of 2 to 7 s, then a small move, one or two views
            // to a side, or back to the middle; never a jump across the whole range.
            o.HeadClock = Organic.Jitter(Rng, 3.6f, 0.6f) + 1.2f;
            o.HeadTarget = Rng.Next(3) == 0 ? 2 : Math.Clamp(o.HeadIdx + (Rng.Next(2) == 0 ? -1 : 1) * (1 + Rng.Next(2)), 0, 4);
        }
        o.LookClock += dt;
        if (o.HeadIdx != o.HeadTarget && ((int)(o.T * 9) != (int)((o.T - dt) * 9))) o.HeadIdx += Math.Sign(o.HeadTarget - o.HeadIdx);
        // A shake after hauling out: a quick whole-body shiver with droplets flying off (Present draws the shiver).
        if (o.ShakeT > 0f)
        {
            o.ShakeT -= dt;
            if (_fx.NextDouble() < dt * 34) Drip(o.X + (float)(_fx.NextDouble() - 0.5) * 0.5f * _p, o.Y, 0.9f);
        }
        IdleLife(o, dt, 0.40f, 0.17f);
        if (o.M.Over && o.ScratchT <= 0f) Decide(o);
    }

    /// <summary>
    /// A standing animal is never still: it blinks every few seconds, flicks an ear now and then, twitches its whiskers,
    /// curls the tip of its tail, scratches itself with a hind foot, and breathes a small puff of vapour (it is cold).
    /// Each is a page variant picked here and drawn by Present; they only start while the head is near forward (the
    /// variant pages are painted for the three middle head turns).
    /// </summary>
    private void IdleLife(Otter o, float dt, float breathFwd, float breathUp)
    {
        o.Var = -1;
        bool calm = o.M.Doing == Doing.Rest && o.HeadIdx >= 1 && o.HeadIdx <= 3 && o.ShakeT <= 0f;
        if (o.M.Doing == Doing.Rest)
        {
            if (o.ScratchT > 0f) o.ScratchT -= dt;
            o.NextScratch -= dt;
            if (calm && o.NextScratch <= 0f && o.T > 0.8 && o.M.Left > 1.6f && o.ScratchT <= 0f && !Lull)
            {
                o.ScratchT = 1.2f; o.NextScratch = Organic.Jitter(Rng, 20f, 0.8f) + 6f; o.HeadTarget = 2;
            }
            if (o.ScratchT <= 0f)
            {
                o.NextBlink -= dt; o.NextEar -= dt; o.NextWhisk -= dt; o.NextCurl -= dt;
                if (calm)
                {
                    if (o.NextBlink <= 0f) { o.BlinkT = 0.16f; o.NextBlink = Organic.Jitter(Rng, 2.6f, 0.8f) + 0.8f; }
                    if (o.NextEar <= 0f) { o.EarT = 0.28f; o.NextEar = Organic.Jitter(Rng, 6f, 0.8f) + 2f; }
                    if (o.NextWhisk <= 0f) { o.WhiskT = 0.5f; o.NextWhisk = Organic.Jitter(Rng, 4.5f, 0.8f) + 1.5f; }
                    if (o.NextCurl <= 0f) { o.CurlT = 1.4f; o.NextCurl = Organic.Jitter(Rng, 9f, 0.8f) + 3f; }
                }
                if (o.BlinkT > 0f) { o.BlinkT -= dt; o.Var = 0; }
                else if (o.EarT > 0f) { o.EarT -= dt; o.Var = 1; }
                else if (o.WhiskT > 0f) { o.WhiskT -= dt; o.Var = ((int)(o.WhiskT * 14f) & 1) == 0 ? 2 : -1; }
                else if (o.CurlT > 0f) { o.CurlT -= dt; o.Var = 3; }
            }
        }
        // Breath: a small puff of vapour from the nose every few seconds.
        o.NextBreath -= dt;
        if (o.NextBreath <= 0f)
        {
            o.NextBreath = Organic.Jitter(Rng, 3.0f, 0.7f) + 1.0f;
            float fwd = breathFwd * _p * (MathF.Cos(o.Hd) >= 0f ? 1f : -1f) * MathF.Abs(MathF.Cos(o.Hd));
            Puff(o.X + fwd, o.Y + MathF.Sin(o.Hd) * breathFwd * _p * _r, 0.12f, breathUp * _p);
        }
    }

    /// <summary>Rolls what to do next: wander, slide, stand, roll, go for a swim, or fish; weights set by the dials and the situation.</summary>
    private void Decide(Otter o)
    {
        if (_force is "wrestle" or "chase" or "fish" && o.I > 0) { Begin(o, Doing.Rest, 5f); return; }   // the test: the playmate stays put until the slider comes by
        Span<float> w = stackalloc float[8];
        bool quiet = Lull;
        w[0] = 0.17f * (1f - 0.4f * o.Lazy);                                                    // wander
        w[1] = quiet ? 0f : 0.34f * (0.35f + o.Playful);                                         // slide
        w[2] = 0.12f * (0.5f + o.Lazy);                                                          // stand and look
        w[3] = quiet ? 0f : 0.12f * (0.5f + o.Lazy);                                             // roll
        w[4] = 0.10f + 0.012f * Math.Min(o.OutTime, 50f);                                        // go for a swim
        w[5] = _count > 0 && _fishClock > 25f && !AnyFish() ? 0.12f + 0.006f * Math.Min(o.OutTime, 40f) : 0f;   // go fishing
        w[6] = 0.22f;                                                                           // keep still a bit longer
        w[7] = quiet || _banks.Count == 0 ? 0f : 0.20f * (0.4f + o.Playful);                    // the snow slide
        int decided = Dice.Pick(Rng, w);
        if (_log != null) Log($"{o.I} decide {decided}");
        switch (decided)
        {
            case 0:
                {
                    for (int tries = 0; tries < 12; tries++)
                    {
                        float a = (float)Rng.NextDouble() * MathF.Tau, d = 0.25f + 0.55f * (float)Rng.NextDouble();
                        float x = _cx + MathF.Cos(a) * _rx * d, y = _cy + MathF.Sin(a) * _ry * d;
                        if (Allowed(x, y) && !S.Crowded(x, y, 0.5f * _p, this, o.I) && PlaneDist(o.X, o.Y, x, y) > 1.5f * _p && PlaneDist(o.X, o.Y, x, y) < 5.0f * _p) { StartGallop(o, x, y, After.Rest, false, 0f, false); return; }
                    }
                    Begin(o, Doing.Rest, 2f); break;
                }
            case 1: StartSlide(o); break;
            case 2: StartStand(o, float.NaN, float.NaN); break;
            case 3: StartRoll(o); break;
            case 4: GoDive(o, false, false); break;
            case 5: GoDive(o, false, true); _fishClock = 0f; break;
            case 7: StartTobog(o); break;
            default: Begin(o, Doing.Rest, Organic.Jitter(Rng, 7f) + 2f); break;
        }
    }

    private bool AnyFish()
    {
        foreach (Otter q in _o) if (q.Fish || q.WantFish || q.M.Doing == Doing.Eat) return true;
        return false;
    }

    // ---------------------------------------------------------------- GALLOP

    private void StartGallop(Otter o, float gx, float gy, After next, bool panic, float finalHd, bool haveFinal = true)
    {
        o.HasWay = false; o.PassedWay = false;
        o.GoalX = gx; o.GoalY = gy; o.Next = next; o.Panic = panic; o.HaveGoalHd = haveFinal; o.GoalHd = finalHd; o.Chasing = false; o.Braked = false;
        Begin(o, Doing.Gallop);
        o.Pace.Halt();                                       // the pace starts from nothing (the log showed the slide's last speed on a standing gallop)
        float L = PlaneDist(o.X, o.Y, gx, gy);
        // Fit the stride to the distance: a whole number of strides, each within reach of an otter's real one.
        int n = Math.Max(2, (int)MathF.Round(L / _stride));
        o.Stride = L < 0.55f * _p ? 0f : Math.Clamp(L / n, 0.5f * _stride, 1.5f * _stride);
        if (_log != null) Log($"{o.I} gallop to {gx:F0},{gy:F0} L={L / _p:F1}m n={n} next={next} panic={panic}");
    }

    private void UpdateGallop(Otter o, float dt)
    {
        float dx = o.GoalX - o.X, dz = (o.GoalY - o.Y) / _r, dist = MathF.Sqrt(dx * dx + dz * dz);
        if (o.Chasing && o.Partner >= 0) { Otter a = _o[o.Partner]; o.GoalX = a.X; o.GoalY = a.Y; dx = o.GoalX - o.X; dz = (o.GoalY - o.Y) / _r; dist = MathF.Sqrt(dx * dx + dz * dz); }
        if (o.Chasing && o.Partner >= 0 && dist < 1.0f * _p && CatchableNow(_o[o.Partner])) { StartWrestle(_o[o.Partner], o); return; }
        float speed = (o.Panic ? 2.4f : o.Chasing ? 2.1f : 1.65f) * _p;
        float stride = o.Stride > 0 ? o.Stride : _stride;
        switch (o.Stage)
        {
            case 0:                                                                              // turn on the spot to face the goal
                {
                    float tgt = dist > 0.1f * _p ? MathF.Atan2(dz, dx) : o.Hd;
                    float err = Turn.Error(o.Hd, tgt);
                    o.Hd = Turn.Toward(o.Hd, tgt, o.Panic ? 5f : 3.2f, dt);
                    if (MathF.Abs(err) < 0.4f || dist < 0.55f * _p) { o.Stage = o.Stride <= 0f ? 3 : 1; o.T = 0; if (o.Stride <= 0f) { o.Pace.Halt(); } }
                    break;
                }
            case 1:                                                                              // the crouch before a run
                if (o.T >= (o.Panic ? 0.13 : 0.22)) { o.Stage = 2; o.T = 0; o.Pace.Walked = 0f; o.Pace.Halt(); o.Pace.Accel = (o.Panic ? 7f : 4.5f) * _p; }
                break;
            case 2:
                {
                    float tgt = MathF.Atan2(dz, dx);
                    tgt = AroundHole(o, tgt);
                    float steer = Clear(o, tgt);
                    o.Hd = Turn.Toward(o.Hd, steer, 2.6f, dt);
                    if (!o.Pace.Braking && !o.Braked) o.Pace.Want = speed;
                    float d = o.Pace.Tick(dt);
                    // BLOCKED (rim, hole, fir): the pace drops to a stop and builds up again along the heading as it turns toward the
                    // way round. Only if it stays blocked for 0.7 s does the run end (the old rule ended it on the FIRST refused
                    // step, which a slide that had just bumped the hole's ring made every time: 11 gallops in 3 minutes died at once).
                    if (Move(o, d)) o.Blocked = 0f;
                    else { o.Pace.Halt(); o.Blocked += dt; if (o.Blocked > 0.7f) { o.Chasing = false; o.Braked = true; o.Blocked = 0f; } }
                    if (!o.Chasing && !o.Braked && o.Pace.Speed > 0.35f * _p)
                    {
                        // The stride was fitted so that (distance walked + distance left) is a whole number of strides: braking
                        // over EXACTLY the distance left therefore ends on the rest page. It starts when the run-down would take
                        // under about 0.9 s (a brake from a crawl over a long way is a slow crawl, not a stop).
                        if (dist <= MathF.Max(0.2f * stride, 0.45f * o.Pace.Speed)) { o.Pace.Brake(MathF.Max(1f, dist)); o.Braked = true; }
                    }
                    if (o.Chasing)
                    {
                        // The chaser runs the other down; when it is close enough the two tumble together.
                        Otter a = _o[o.Partner];
                        if (dist < 1.3f * _p && !CatchableNow(a)) o.Pace.Want = 0.45f * _p;           // close behind a playmate still sliding: trail it
                        if (o.T > 9f || a.M.Doing == Doing.Under) { o.Chasing = false; o.Braked = false; }
                    }
                    if (o.Braked && !o.Pace.Braking && o.Pace.Speed <= 0.5f) { o.Stage = 3; o.T = 0; }
                    break;
                }
            case 3:                                                                              // the stop: set the feet down and settle
                if (o.T >= (o.Panic ? 0.12 : 0.22)) { o.Stage = 4; o.T = 0; }
                break;
            case 4:                                                                              // face the way the next thing needs
                {
                    if (o.HaveGoalHd) o.Hd = Turn.Toward(o.Hd, o.GoalHd, o.Panic ? 5f : 3.2f, dt);
                    if (!o.HaveGoalHd || MathF.Abs(Turn.Error(o.Hd, o.GoalHd)) < 0.06f) Arrive(o);
                    break;
                }
        }
    }

    /// <summary>
    /// The hole is open water in the middle of the ice: when the straight way to the goal crosses it, steer for a point above or
    /// below it first (in the flat plane the hole is a tall oval, so turning a few degrees is not enough), then on to the goal.
    /// </summary>
    private float AroundHole(Otter o, float tgt)
    {
        if (o.PassedWay) return tgt;
        bool blocked = false;
        for (int k = 1; k <= 12 && !blocked; k++)
        {
            float f = k / 12f;
            if (InHole(o.X + (o.GoalX - o.X) * f, o.Y + (o.GoalY - o.Y) * f, 1.35f)) blocked = true;
        }
        if (!blocked && !o.HasWay) return tgt;
        if (!o.HasWay)
        {
            float side = (o.Y + o.GoalY) / 2 >= _hy ? 1f : -1f;
            float wy = _hy + side * _hry * 2.0f;
            if (!Allowed(_hx, wy)) { side = -side; wy = _hy + side * _hry * 2.0f; }
            o.WayX = _hx; o.WayY = wy; o.HasWay = true;
        }
        if (PlaneDist(o.X, o.Y, o.WayX, o.WayY) < 0.6f * _p || (o.HasWay && MathF.Sign(o.X - _hx) != MathF.Sign(o.GoalX - _hx) && MathF.Abs(o.X - _hx) > _hrx * 1.1f))
        { o.PassedWay = true; o.HasWay = false; return tgt; }
        return MathF.Atan2((o.WayY - o.Y) / _r, o.WayX - o.X);
    }

    private void Arrive(Otter o)
    {
        o.Panic = false; o.Chasing = false;
        // A gallop that was cut short (a prop or the rim in the way) must not hand over to a doing that expects the otter AT its spot.
        if (o.Next != After.Rest && o.Next != After.Beg && o.Next != After.Wrestle && PlaneDist(o.X, o.Y, o.GoalX, o.GoalY) > 1.0f * _p)
        { if (_log != null) Log($"{o.I} gallop for {o.Next} ended {PlaneDist(o.X, o.Y, o.GoalX, o.GoalY) / _p:F1} lengths short"); Begin(o, Doing.Rest, Organic.Jitter(Rng, 2f)); return; }
        switch (o.Next)
        {
            case After.Dive: case After.DiveFish:
                if (o.Face == 0) o.Face = Math.Cos(o.Hd) >= 0 ? 1 : -1;
                Begin(o, Doing.Dive, 1.4f); if (_log != null) Log($"{o.I} dives"); break;
            case After.Roll: StartRollNow(o); break;
            case After.Slide: BeginSlide(o); break;
            case After.Tobog: BeginTobog(o); break;
            case After.Beg: StartStand(o, _o.Length > 0 && OtherEating(o, out Otter e) ? e.X : float.NaN, float.NaN, true); break;
            default: Begin(o, Doing.Rest, Organic.Jitter(Rng, 6f) + 1.5f); break;
        }
    }

    // ---------------------------------------------------------------- SLIDE (the signature)

    private void StartSlide(Otter o)
    {
        // A slide needs a long clear run across the ice (about six metres: three or four bounds, the flop, and a glide that
        // dies away over three more). So pick, from eight tries, the start and heading with the longest chord of clear ice
        // (nearly along the pond's long way, a little luck in it), gallop to the start first, and begin there.
        float bestScore = -1f, bh = 0f, bsx = o.X, bsy = o.Y, bchord = 0f;
        for (int k = 0; k < 8; k++)
        {
            float h = (k % 2 == 0 ? 0f : MathF.PI) + (float)(Rng.NextDouble() - 0.5) * 0.9f;
            float qx = _cx + (float)(Rng.NextDouble() * 2 - 1) * 0.35f * _rx, qy = _cy + (float)(Rng.NextDouble() * 2 - 1) * 0.35f * _ry;
            if (!Allowed(qx, qy)) continue;
            float cs = MathF.Cos(h), sn = MathF.Sin(h) * _r;
            float bk = 0f; while (bk < 9f * _p && Allowed(qx - cs * bk, qy - sn * bk)) bk += 0.1f * _p;
            float fw = 0f; while (fw < 9f * _p && Allowed(qx + cs * fw, qy + sn * fw)) fw += 0.1f * _p;
            float back = MathF.Max(0f, bk - 0.35f * _p), chord = back + fw;
            float score = chord + (float)Rng.NextDouble() * 0.8f * _p;
            if (score > bestScore) { bestScore = score; bh = h; bsx = qx - cs * back; bsy = qy - sn * back; bchord = chord; }
        }
        o.Chord = bchord; o.Slides = 0;
        o.Spin = (Rng.Next(2) == 0 ? 1f : -1f) * (0.22f + 0.22f * (float)Rng.NextDouble());
        if (PlaneDist(o.X, o.Y, bsx, bsy) > 0.9f * _p) StartGallop(o, bsx, bsy, After.Slide, false, bh);
        else { o.GoalHd = bh; BeginSlide(o); }
    }

    /// <summary>
    /// THE SNOW SLIDE: gallop to the foot of a drift just outside the near rim, climb it (the feet lift, the shadow stays on the
    /// ground, so the body rises), turn at the top, drop onto the belly and toboggan down onto the ice, picking up speed into a
    /// long glide. Stages of the Slide doing: 6 climb, 7 turn at the top, 8 flop onto the belly, 9 the run down (then 4, the glide).
    /// </summary>
    private void StartTobog(Otter o)
    {
        if (_banks.Count == 0) { StartSlide(o); return; }
        var bk = _banks[Rng.Next(_banks.Count)];
        o.BX = bk.Bx; o.BY = bk.By; o.TX = bk.Tx; o.TY = bk.Ty; o.Spin = (Rng.Next(2) == 0 ? 1f : -1f) * (0.22f + 0.22f * (float)Rng.NextDouble());
        StartGallop(o, bk.Bx, bk.By, After.Tobog, false, bk.Th);               // always walk there (no snap): a short way is a turn and a settle
    }

    private void BeginTobog(Otter o)
    {
        Begin(o, Doing.Slide);
        o.BX = o.X; o.BY = o.Y;                                                // the climb starts from wherever the otter ended up standing
        o.Stage = 6; o.Lift = 0f; o.LiftMax = 0.17f;
        o.Pace.Accel = 5f * _p; o.Pace.Halt(); o.Pace.Walked = 0f;
        if (_log != null) Log($"{o.I} climbs the drift at {o.TX:F0},{o.TY:F0}");
    }

    private void UpdateTobog(Otter o, float dt)
    {
        switch (o.Stage)
        {
            case 6:
                {
                    o.Pace.Want = 0.9f * _p;
                    float d = o.Pace.Tick(dt);
                    o.X += MathF.Cos(o.Hd) * d; o.Y += MathF.Sin(o.Hd) * d * _r;
                    float total = PlaneDist(o.TX, o.TY, o.BX, o.BY), prog = total > 1f ? PlaneDist(o.X, o.Y, o.BX, o.BY) / total : 1f;
                    o.Lift = o.LiftMax * Sm(prog);
                    if (prog >= 1f || o.T > 4.0)
                    {
                        o.Pace.Halt(); o.Lift = o.LiftMax; o.Stage = 7; o.T = 0;
                        // The way down: back toward the middle of the pond, fanned a little to the side, whichever has the longest clear ice.
                        float baseH = o.Hd + MathF.PI, bestH = baseH, bestS = -1f;
                        for (int k = -2; k <= 2; k++)
                        {
                            float h = baseH + k * 0.3f, cs = MathF.Cos(h), sn = MathF.Sin(h) * _r, len = 0f;
                            while (len < 9f * _p && Allowed(o.X + cs * len, o.Y + sn * len)) len += 0.25f * _p;
                            float score = len + (float)Rng.NextDouble() * 1.2f * _p;
                            if (score > bestS) { bestS = score; bestH = h; }
                        }
                        o.TobogHd = bestH;
                    }
                    break;
                }
            case 7:
                {
                    o.Hd = Turn.Toward(o.Hd, o.TobogHd, 3.0f, dt);
                    if (o.T >= 0.2 && MathF.Abs(Turn.Error(o.Hd, o.TobogHd)) < 0.08f) { o.Stage = 8; o.T = 0; }
                    break;
                }
            case 8:
                if (o.T >= 0.4) { o.Stage = 9; o.T = 0; o.Speed = 0.3f * _p; }
                break;
            case 9:
                {
                    float u = (float)Math.Min(1.0, o.T / 0.8);
                    o.Speed = (0.3f + 2.0f * Sm(u)) * _p;
                    o.Lift = o.LiftMax * (1f - Sm((float)Math.Min(1.0, o.T / 0.55)));
                    if (!Move(o, o.Speed * dt)) o.Speed = 0f;
                    if (o.T > 0.1) AddScratch(o.X, o.Y);
                    if (u >= 1f)
                    {
                        o.Lift = 0f; o.Stage = 4; o.T = 0;
                        float cz = (_cy - o.Y) / _r, cxx = _cx - o.X, cross = MathF.Cos(o.Hd) * cz - MathF.Sin(o.Hd) * cxx;
                        o.Spin = MathF.Abs(o.Spin) * (cross >= 0 ? 1f : -1f);
                        Puff(o.X, o.Y, 0.4f);
                        Board.Post(Sense.Movement, new PointF(o.X, o.Y), 0.3f * S.U, 0.4f, o);
                        if (_log != null) Log($"{o.I} glide begins v={o.Speed / _p:F2} m/s (toboggan)");
                        TryChase(o);
                    }
                    break;
                }
        }
    }

    private void BeginSlide(Otter o)
    {
        // Bounds in the run-up: as many as the clear ice allows after the flop and the glide, three or four if it can.
        float spare = o.Chord / _p - 0.3f - 0.6f - 3.0f;
        o.RunN = Math.Clamp(MathF.Floor(spare / 0.8f - 0.56f), 2f, 4f);
        Begin(o, Doing.Slide);
        o.Pace.Accel = 5f * _p; o.Pace.Halt();
        if (_log != null) Log($"{o.I} slide: chord {o.Chord / _p:F1} m, {o.RunN} bounds");
    }
    private void UpdateSlide(Otter o, float dt)
    {
        // Stages: 0 turn to the launch heading, 1 crouch, 2 run-up, 3 belly flop, 4 glide, 5 scramble up.
        if (o.Stage >= 6 && o.Stage <= 9) { UpdateTobog(o, dt); return; }
        switch (o.Stage)
        {
            case 0:
                {
                    float err = Turn.Error(o.Hd, o.GoalHd);
                    o.Hd = Turn.Toward(o.Hd, o.GoalHd, 3.2f, dt);
                    if (MathF.Abs(err) < 0.1f) { o.Stage = 1; o.T = 0; }
                    break;
                }
            case 1:
                if (o.T >= 0.22) { o.Stage = 2; o.T = 0; o.Pace.Walked = 0f; o.Pace.Halt(); }
                break;
            case 2:
                {
                    o.Pace.Want = 1.75f * _p;
                    float d = o.Pace.Tick(dt);
                    if (!Move(o, d)) { o.Stage = 3; o.T = 0; o.Speed = o.Pace.Speed; break; }          // out of ice already: flop here
                    if (o.Pace.Walked >= (o.RunN + 9f / 16f) * _stride) { o.Stage = 3; o.T = 0; o.Speed = o.Pace.Speed; }
                    break;
                }
            case 3:
                {
                    if (!Move(o, o.Speed * dt)) { o.Speed = 0f; }
                    if (o.T >= 0.34)
                    {
                        o.Stage = 4; o.T = 0;
                        // Curve toward the middle of the pond (never out toward the rim): the side the centre is on.
                        { float cz = (_cy - o.Y) / _r, cxx = _cx - o.X; float cross = MathF.Cos(o.Hd) * cz - MathF.Sin(o.Hd) * cxx; o.Spin = MathF.Abs(o.Spin) * (cross >= 0 ? 1f : -1f); }
                        Puff(o.X, o.Y, 0.4f); Puff(o.X - MathF.Cos(o.Hd) * 0.2f * _p, o.Y, 0.3f);
                        // The slide is seen by the others: pinned once.
                        Board.Post(Sense.Movement, new PointF(o.X, o.Y), 0.3f * S.U, 0.4f, o);
                        if (_log != null) Log($"{o.I} glide begins v={o.Speed / _p:F2} m/s");
                        TryChase(o);
                    }
                    break;
                }
            case 4:
                {
                    o.Speed *= MathF.Exp(-0.5f * dt);
                    o.Hd += o.Spin * dt * Math.Min(1f, o.Speed / (0.9f * _p));                    // the glide curves: slowly spinning
                    o.Hd = MathF.IEEERemainder(o.Hd, MathF.Tau);
                    if (!Move(o, o.Speed * dt))
                    {
                        // Bumps the snow rim: a small puff, and stops.
                        Puff(o.X + MathF.Cos(o.Hd) * 0.3f * _p, o.Y, 0.5f); o.Speed = 0f;
                        if (_log != null) Log($"{o.I} bumped the rim");
                    }
                    if (o.T > 0.05) AddScratch(o.X, o.Y);
                    if (o.Speed < 0.25f * _p) { o.Stage = 5; o.T = 0; o.Speed = 0f; }
                    break;
                }
            case 5:
                if (o.T >= 0.55)
                {
                    o.Slides++;
                    // Up and round for another go, now and then.
                    if (!Lull && o.Slides < 3 && Rng.NextDouble() < 0.35 + 0.35 * o.Playful) StartSlide(o);
                    else Begin(o, Doing.Rest, Organic.Jitter(Rng, 3f));
                    o.Slides = o.M.Doing == Doing.Slide ? o.Slides : 0;
                }
                break;
        }
    }

    /// <summary>When one slides, a playmate (the other otter, if it is free and in the mood) goes after it.</summary>
    private void TryChase(Otter a)
    {
        if (_o.Length < 2 || Lull) return;
        foreach (Otter b in _o)
        {
            if (b == a || b.M.Doing != Doing.Rest || b.Fish) continue;
            if (!(_force is "wrestle" or "chase") && Rng.NextDouble() > 0.55 * (0.5 + b.Playful)) continue;
            b.Partner = a.I; a.Partner = b.I;
            b.GoalX = a.X; b.GoalY = a.Y; b.Next = After.Wrestle; b.Panic = false; b.Braked = false; b.Stride = _stride; b.HaveGoalHd = false;
            Begin(b, Doing.Gallop);
            b.Chasing = true; b.Stage = 0;
            if (_log != null) Log($"{b.I} chases {a.I}");
            return;
        }
    }

    // ---------------------------------------------------------------- WRESTLE
    // The two MEET (no teleport): when the chaser reaches a playmate who is standing, both shuffle to stand nose to nose 0.76 m apart
    // (stage 0), CURL into one rolling ball over three shared pages (1), tumble for two or three whole turns (2), uncurl through the
    // same three pages backwards (3) and step apart (4). The pair is drawn as ONE picture in stages 1 to 3, by the lower-numbered otter.

    private const float PairGap = 0.38f;

    private bool CatchableNow(Otter a) => a.M.Doing == Doing.Rest || (a.M.Doing == Doing.Slide && a.Stage == 5 && a.T > 0.4);

    private void StartWrestle(Otter a, Otter b)
    {
        float mx = (a.X + b.X) / 2, my = (a.Y + b.Y) / 2;
        if (!(S.OnPond(mx, my, 1.18f) && !InHole(mx, my, 1.1f) && PropFree(mx, my, 1.0f * _p))) { b.Chasing = false; return; }                 // a ball may roll on the rim snow, but never into the hole
        Otter left = a.X <= b.X ? a : b, right = left == a ? b : a;
        foreach (Otter q in new[] { a, b })
        {
            Begin(q, Doing.Wrestle);
            q.Speed = 0f; q.Pace.Halt(); q.Chasing = false; q.Panic = false; q.HasLook = false;
        }
        left.GoalX = mx - PairGap * _p; right.GoalX = mx + PairGap * _p; left.GoalY = right.GoalY = my;
        left.GoalHd = 0f; right.GoalHd = MathF.PI;
        a.Partner = b.I; b.Partner = a.I;
        Otter m = a.I < b.I ? a : b;
        m.BallX = mx; m.BallY = my; m.BallTh = 0f; m.BallTurns = 2;
        m.BallVx = (float)(Rng.NextDouble() - 0.5) * 0.3f * _p; m.BallVy = (float)(Rng.NextDouble() - 0.5) * 0.12f * _p;
        if (_log != null) Log($"{a.I} and {b.I} meet to wrestle at {mx:F0},{my:F0}");
    }

    private void UpdateWrestle(Otter o, float dt)
    {
        Otter m = o.I < o.Partner ? o : _o[o.Partner];
        Otter other = o == m ? _o[o.Partner] : m;
        if (o != m)
        {
            // The follower mirrors the pair's stage and (while the ball is one picture) its place.
            o.Stage = m.Stage; o.T = m.T;
            if (m.Stage is >= 1 and <= 3) { o.X = m.X + (o.GoalX - (m.BallX)); o.Y = m.Y; }
        }
        if (o == m)
        {
            switch (m.Stage)
            {
                case 0:
                    {
                        bool ready = true;
                        for (int pi = 0; pi < 2; pi++)               // (a plain loop: no array made every frame)
                        {
                            Otter q = pi == 0 ? m : other;
                            float dx = q.GoalX - q.X, dy = q.GoalY - q.Y, d = MathF.Sqrt(dx * dx + dy * dy), step = 0.8f * _p * dt;
                            if (d > 0.5f) { float k = MathF.Min(1f, step / d); q.X += dx * k; q.Y += dy * k; ready = false; }
                            q.Hd = Turn.Toward(q.Hd, q.GoalHd, 4.5f, dt);
                            if (MathF.Abs(Turn.Error(q.Hd, q.GoalHd)) > 0.08f) ready = false;
                        }
                        if (ready || m.T > 4.0) { m.X = m.BallX; m.Y = m.BallY; m.Stage = 1; m.T = 0; }
                        break;
                    }
                case 1:
                    if (m.T >= 0.3) { m.Stage = 2; m.T = 0; m.BallTh = 0f; Puff(m.X, m.Y, 0.3f); }
                    break;
                case 2:
                    {
                        float ramp = (float)Math.Min(1.0, m.T / 0.5);
                        m.BallTh += dt * 3.4f * ramp;
                        float nx = m.X + m.BallVx * dt, ny = m.Y + m.BallVy * dt;
                        if (S.OnPond(nx, ny, 1.18f) && !InHole(nx, ny, 1.1f) && PropFree(nx, ny, 1.0f * _p)) { m.X = nx; m.Y = ny; } else { m.BallVx = -m.BallVx; m.BallVy = -m.BallVy; }
                        if (_fx.NextDouble() < dt * 5) Puff(m.X + (float)(_fx.NextDouble() - 0.5) * 0.6f * _p, m.Y, 0.25f);
                        if (m.BallTh >= m.BallTurns * MathF.Tau) { m.BallTh = 0f; m.Stage = 3; m.T = 0; }
                        break;
                    }
                case 3:
                    if (m.T >= 0.3)
                    {
                        // Apart: each stands where its half of the pair stood, nose to nose, and the more playful hops back.
                        Otter left = m.GoalX <= other.GoalX ? m : other, right = left == m ? other : m;
                        float bcx = m.X, bcy = m.Y;                                              // (read first: the master is one of the two and is about to be moved)
                        left.X = bcx - PairGap * _p; right.X = bcx + PairGap * _p; left.Y = right.Y = bcy;
                        left.Hd = 0f; right.Hd = MathF.PI;
                        foreach (Otter q in new[] { m, other }) { Begin(q, Doing.Rest, Organic.Jitter(Rng, 2.5f)); q.Partner = -1; q.HasLook = false; }
                        Otter hop = m.Playful >= other.Playful ? m : other;
                        float dir = hop == left ? -1f : 1f;
                        float gx = hop.X + dir * 1.6f * _p;
                        if (Allowed(gx, hop.Y)) StartGallop(hop, gx, hop.Y, After.Rest, false, hop.Hd, false);
                    }
                    break;
            }
        }
    }
    // ---------------------------------------------------------------- STAND

    private void StartStand(Otter o, float lx, float ly, bool beg = false)
    {
        if (!float.IsNaN(lx)) { o.LookX = lx; o.LookY = float.IsNaN(ly) ? o.Y : ly; o.HasLook = true; } else o.HasLook = false;
        o.LookClock = 0f; o.Level = 0;
        Begin(o, Doing.Stand, beg ? 6f : Organic.Jitter(Rng, 4.5f, 0.7f) + 1.2f);
    }

    private void UpdateStand(Otter o, float dt)
    {
        // Stage 0: rise; 1: look; 2: down. A held look follows the thing noticed; with none, it looks about.
        o.Speed = 0f; o.Pace.Halt();
        if (o.Stage == 0 && o.T >= 0.4) { o.Stage = 1; o.T = 0; }
        if (o.Stage == 1)
        {
            o.LookClock += dt;
            if (o.HasLook)
            {
                float tgt = LookHeading(o, 0f);
                o.Hd = Turn.Toward(o.Hd, tgt, 2.0f, dt);
                o.Level = o.LookY < o.Y - 3f * _ry ? 2 : 1;
            }
            else
            {
                o.HeadClock -= dt;
                if (o.HeadClock <= 0f) { o.HeadClock = Organic.Jitter(Rng, 3.2f, 0.6f) + 1.0f; o.GoalHd = o.Hd + (float)(Rng.NextDouble() - 0.5) * 1.4f; o.Level = Math.Clamp(o.Level + Rng.Next(3) - 1, 0, 2); }
                o.Hd = Turn.Toward(o.Hd, o.GoalHd, 1.2f, dt);
            }
            // A long look at Santa is cut short only by the timer.
            if (o.M.Over) { o.Stage = 2; o.T = 0; }
        }
        if (o.Stage == 2 && o.T >= 0.4) { Begin(o, Doing.Rest, Organic.Jitter(Rng, 2.5f)); o.HasLook = false; }
    }

    // ---------------------------------------------------------------- ROLL

    private void StartRoll(Otter o)
    {
        // A spot on the near rim, where the snow is: the lower half of the ellipse.
        for (int tries = 0; tries < 20; tries++)
        {
            float a = (0.15f + 0.7f * (float)Rng.NextDouble()) * MathF.PI;
            float x = _cx + MathF.Cos(a) * _rx * 1.045f, y = _cy + MathF.Sin(a) * _ry * 1.045f;
            if (!Allowed(x, y) || InHole(x, y, 3f)) continue;
            int side = Rng.Next(2) == 0 ? 1 : -1;
            StartGallop(o, x, y, After.Roll, false, side > 0 ? 0f : MathF.PI);
            return;
        }
        Begin(o, Doing.Rest, 2f);
    }

    private void StartRollNow(Otter o)
    {
        Begin(o, Doing.Roll, 0.6f + Organic.Jitter(Rng, 3.6f, 0.5f) + 0.6f);                   // flop, wriggle, flop back
        o.Face = Math.Cos(o.Hd) >= 0 ? 1 : -1;
    }

    private void UpdateRoll(Otter o, float dt)
    {
        o.Speed = 0f;
        o.Hd = o.Face > 0 ? 0f : MathF.PI;
        float total = o.M.Left + (float)o.T;
        if (o.Stage == 0 && o.T >= 0.6) { o.Stage = 1; o.T = 0; }
        if (o.Stage == 1)
        {
            if (_fx.NextDouble() < dt * 2.5) Puff(o.X + (float)(_fx.NextDouble() - 0.5) * 0.6f * _p, o.Y, 0.2f);
            if (o.M.Left <= 0.6f) { o.Stage = 2; o.T = 0; }
        }
        if (o.Stage == 2 && o.T >= 0.6) Begin(o, Doing.Rest, Organic.Jitter(Rng, 2.5f));
    }

    // ---------------------------------------------------------------- EAT

    private void UpdateEat(Otter o, float dt)
    {
        // Stage 0: sit up (0.55 s); 1..3: three bites; 4: the end (a last wipe and down, 0.5 s).
        o.Speed = 0f;
        o.Hd = o.Face > 0 ? 0f : MathF.PI;
        if (o.Stage == 0 && o.T >= 0.55) { o.Stage = 1; o.T = 0; }
        else if (o.Stage >= 1 && o.Stage <= 3)
        {
            double dur = o.Stage == 3 ? 1.5 : 2.1;
            if (o.T >= dur) { o.Stage++; o.T = 0; if (o.Stage <= 3) for (int i = 0; i < 3; i++) Drip(o.X + o.Face * 0.12f * _p, o.Y - 0.2f * _p, 0.35f); }
        }
        else if (o.Stage == 4 && o.T >= 0.55)
        {
            o.Fish = false;
            Begin(o, Doing.Rest, Organic.Jitter(Rng, 3f));
        }
    }

    // ---------------------------------------------------------------- keeping apart

    private void Separate(float dt)
    {
        for (int i = 0; i < _o.Length; i++)
            for (int j = i + 1; j < _o.Length; j++)
            {
                Otter a = _o[i], b = _o[j];
                if (!OnGround(a) || !OnGround(b)) continue;
                float dx = b.X - a.X, dz = (b.Y - a.Y) / _r, d = MathF.Sqrt(dx * dx + dz * dz), room = 0.8f * _p;
                if (d >= room) continue;
                if (d < 1e-3f) { dx = 1f; dz = 0; d = 1f; }
                float push = (room - d) * Math.Min(1f, dt * 4f) * 0.5f;
                float ux = dx / d, uz = dz / d;
                if (Allowed(a.X - ux * push, a.Y - uz * push * _r)) { a.X -= ux * push; a.Y -= uz * push * _r; }
                if (Allowed(b.X + ux * push, b.Y + uz * push * _r)) { b.X += ux * push; b.Y += uz * push * _r; }
            }
    }

    private static bool OnGround(Otter o) => o.M.Doing is Doing.Rest or Doing.Gallop or Doing.Stand or Doing.Roll or Doing.Eat || (o.M.Doing == Doing.Slide);

    // ---------------------------------------------------------------- the test dial

    private void Force()
    {
        Otter o = _o[0];
        if (_log != null) Log($"forced {_force}");
        switch (_force!.ToLowerInvariant())
        {
            case "slide": StartSlide(o); break;
            case "tobog": StartTobog(o); break;
            case "chase": StartSlide(o); break;
            case "gallop": StartGallop(o, _cx + 0.5f * _rx, _cy + 0.3f * _ry, After.Rest, false, 0f, false); break;
            case "stand": StartStand(o, float.NaN, float.NaN); break;
            case "roll": StartRoll(o); break;
            case "dive": GoDive(o, false, false); break;
            case "fish": GoDive(o, false, true); break;
            case "flee": Board.Post(Sense.Threat, new PointF(o.X, o.Y), 1.2f * S.U, 1f, null); break;
            case "surface": o.M.Begin(Doing.Under, 0.1f); break;
            case "wrestle": StartSlide(o); break;
        }
    }

    // ---------------------------------------------------------------- the picture

    private void Present(Otter o)
    {
        OtterPages g = _pg!;
        o.Vis = true; o.ShadowBin = -1; o.ShakeDX = 0f;
        o.FX = o.X; o.FY = o.Y;
        ComputeYaw(o.Hd, out int bin, out bool mir);
        o.Mirror = mir; o.OX = g.OX; o.OY = g.OY;
        switch (o.M.Doing)
        {
            case Doing.Under: o.Vis = false; break;
            case Doing.Surface:
                {
                    // The head rises over 0.7 s, holds, and sinks again over 0.6 s.
                    int lvl = o.Stage == 0 ? (int)(Math.Min(1.0, o.T / 0.7) * 0.999 * 5) : o.Stage >= 3 ? 5 - (int)(Math.Min(1.0, o.T / 0.6) * 0.999 * 5.99) : 5;
                    if (o.Stage == 4) lvl = 5;
                    lvl = Math.Clamp(lvl, 0, 5);
                    o.Spr = g.Surf[lvl, bin, o.Fish ? 1 : 0]; o.OX = g.SurfOX; o.OY = g.SurfOY; o.PageKind = "surf"; o.PageIdx = lvl;
                    break;
                }
            case Doing.Haul:
                {
                    int i = Math.Min(OtterPages.HaulPages - 1, (int)(o.T / 1.7 * OtterPages.HaulPages));
                    o.Spr = g.Haul[i, o.Fish ? 1 : 0]; o.OX = g.WideOX; o.Mirror = o.Face < 0; o.PageKind = "haul"; o.PageIdx = i;
                    o.ShadowBin = i >= 12 ? 4 : -1;
                    break;
                }
            case Doing.Dive:
                {
                    int i = Math.Min(OtterPages.DivePages - 1, (int)(Math.Min(1.0, o.T / 1.35) * OtterPages.DivePages));
                    o.Spr = g.Dive[i]; o.OX = g.DiveOX; o.Mirror = o.Face < 0; o.PageKind = "dive"; o.PageIdx = i;
                    o.ShadowBin = i < 5 ? 4 : -1;
                    break;
                }
            case Doing.Rest:
                if (o.ScratchT > 0f)
                {
                    // Scratching with a hind foot: ramp up, stroke, stroke, ... ramp down (pages 0, 1, 2, 1, 2, 1, 2, 3).
                    int step = Math.Min(7, (int)((1.2f - o.ScratchT) / 0.15f));
                    int sp = step == 0 ? 0 : step == 7 ? 3 : (step % 2 == 1 ? 1 : 2);
                    o.Spr = g.Scratch[sp, bin]; o.PageKind = "scratch"; o.PageIdx = sp;
                }
                else if (o.Var >= 0) { o.Spr = g.RestVar[o.Var, Math.Clamp(o.HeadIdx, 1, 3) - 1, bin]; o.PageKind = "restvar"; o.PageIdx = o.Var; }
                else { o.Spr = g.Rest[o.HeadIdx, bin]; o.PageKind = "rest"; o.PageIdx = o.HeadIdx; }
                o.ShadowBin = bin;
                break;
            case Doing.Gallop:
                switch (o.Stage)
                {
                    case 0: case 4: o.Spr = g.Rest[2, bin]; o.PageKind = "turn"; o.PageIdx = 0; break;
                    case 1: { int i = Math.Min(4, (int)(o.T / (o.Panic ? 0.13 : 0.22) * 5)); o.Spr = g.Start[i, bin]; o.PageKind = "start"; o.PageIdx = i; break; }
                    case 3: { int i = Math.Max(0, 4 - (int)(o.T / (o.Panic ? 0.12 : 0.22) * 5)); o.Spr = g.Start[i, bin]; o.PageKind = "stop"; o.PageIdx = i; break; }
                    default: { int pg = o.Pace.Page(o.Stride > 0 ? o.Stride : _stride, OtterPages.GallopPages); o.Spr = g.Gallop[pg, bin]; o.PageKind = "gal"; o.PageIdx = pg; break; }
                }
                o.ShadowBin = bin;
                break;
            case Doing.Slide:
                switch (o.Stage)
                {
                    case 6: { int pg = o.Pace.Page(_stride, OtterPages.GallopPages); o.Spr = g.Gallop[pg, bin]; o.PageKind = "climb"; o.PageIdx = pg; break; }
                    case 7: o.Spr = g.Rest[2, bin]; o.PageKind = "top"; o.PageIdx = 0; break;
                    case 8: { int i = Math.Max(0, 5 - (int)(o.T / 0.4 * 6)); o.Spr = g.GetUp[i, bin]; o.PageKind = "flop"; o.PageIdx = i; break; }
                    case 9: { int i = (int)(o.T * 4.5) % 4; o.Spr = g.Glide[i, bin]; o.PageKind = "toboggan"; o.PageIdx = i; break; }
                    case 0: o.Spr = g.Rest[2, bin]; o.PageKind = "turn"; o.PageIdx = 0; break;
                    case 1: { int i = Math.Min(4, (int)(o.T / 0.22 * 5)); o.Spr = g.Start[i, bin]; o.PageKind = "start"; o.PageIdx = i; break; }
                    case 2: { int pg = o.Pace.Page(_stride, OtterPages.GallopPages); o.Spr = g.Gallop[pg, bin]; o.PageKind = "gal"; o.PageIdx = pg; break; }
                    case 3: { int i = Math.Min(5, (int)(o.T / 0.34 * 6)); o.Spr = g.Throw[i, bin]; o.PageKind = "throw"; o.PageIdx = i; break; }
                    case 4: { int i = (int)(o.T * 4.5) % 4; o.Spr = g.Glide[i, bin]; o.PageKind = "glide"; o.PageIdx = i; break; }
                    default: { int i = Math.Min(5, (int)(o.T / 0.55 * 6)); o.Spr = g.GetUp[i, bin]; o.PageKind = "getup"; o.PageIdx = i; break; }
                }
                o.ShadowBin = bin;
                break;
            case Doing.Stand:
                if (o.Stage == 0) { int i = Math.Min(4, (int)(o.T / 0.4 * 5)); o.Spr = g.Stand[i, bin]; o.PageKind = "rise"; o.PageIdx = i; }
                else if (o.Stage == 2) { int i = Math.Max(0, 4 - (int)(o.T / 0.4 * 5)); o.Spr = g.Stand[i, bin]; o.PageKind = "lower"; o.PageIdx = i; }
                else { o.Spr = g.Up[Math.Clamp(o.Level, 0, 2), bin]; o.PageKind = "up"; o.PageIdx = o.Level; }
                o.ShadowBin = bin;
                break;
            case Doing.Roll:
                o.Mirror = o.Face < 0;
                if (o.Stage == 0) { int i = Math.Min(5, (int)(o.T / 0.6 * 6)); o.Spr = g.Flop[i]; o.PageKind = "flop"; o.PageIdx = i; }
                else if (o.Stage == 2) { int i = Math.Max(0, 5 - (int)(o.T / 0.6 * 6)); o.Spr = g.Flop[i]; o.PageKind = "unflop"; o.PageIdx = i; }
                else { int i = (int)(o.T * 11) % OtterPages.RollPages; o.Spr = g.RollCycle[i]; o.PageKind = "roll"; o.PageIdx = i; }
                o.ShadowBin = 4;
                break;
            case Doing.Eat:
                o.Mirror = o.Face < 0;
                if (o.Stage == 0) { int i = Math.Min(4, (int)(o.T / 0.55 * 5)); o.Spr = g.SitDown[i]; o.PageKind = "sit"; o.PageIdx = i; }
                else if (o.Stage <= 3) { int c = (int)(o.T * 6) % 8; o.Spr = g.SitEat[o.Stage - 1, c]; o.PageKind = "eat"; o.PageIdx = (o.Stage - 1) * 10 + c; }
                else { int i = Math.Min(4, (int)(o.T / 0.55 * 5)); o.Spr = g.EatEnd[i]; o.PageKind = "eatend"; o.PageIdx = i; }
                o.ShadowBin = 4;
                break;
            case Doing.Wrestle:
                {
                    Otter a = o.I < o.Partner ? o : _o[o.Partner];
                    o.Mirror = false; o.ShadowBin = 4;
                    if (a.Stage == 1) { int i = Math.Min(2, (int)(a.T / 0.1)); o.Spr = g.PairIn[i]; o.PageKind = "pairin"; o.PageIdx = i; }
                    else if (a.Stage == 3) { int i = Math.Max(0, 2 - (int)(a.T / 0.1)); o.Spr = g.PairIn[i]; o.PageKind = "pairout"; o.PageIdx = i; }
                    else if (a.Stage == 2) { int i = (int)((a.BallTh / MathF.Tau % 1f + 1f) % 1f * 12) % 12; o.Spr = g.Ball[i]; o.PageKind = "ball"; o.PageIdx = i; }
                    else { o.Spr = g.Rest[2, bin]; o.Mirror = mir; o.ShadowBin = bin; o.PageKind = "meet"; o.PageIdx = 0; }   // stage 0: nose to nose, two otters drawn singly
                }
                break;
        }
        // On the drift: the feet are lifted off the ground (the shadow stays down, so the body reads as high).
        if (o.M.Doing == Doing.Slide && o.Stage >= 6 && o.Stage <= 9 && o.Lift > 0f) o.OY += (int)(o.Lift * _p);
        // Rest after a haul-out: a shiver of the whole body with droplets flying (the first second).
        if (o.M.Doing == Doing.Rest && o.ShakeT > 0f)
            o.ShakeDX = MathF.Sin(o.ShakeT * 48f) * 2.2f * (o.ShakeT / 0.9f);
    }

    // ---------------------------------------------------------------- drawing

    public override void Collect(DepthQueue q)
    {
        if (_o.Length == 0 || _pg == null) return;
        q.Add(S.Pond.At.Y - S.Pond.Ry, this, 1000);                              // the ice's own marks (ripples, bubbles, scratches): under every animal
        foreach (Otter o in _o)
        {
            if (o.M.Doing == Doing.Wrestle && o.I != Math.Min(o.I, o.Partner) && _o[o.Partner].Stage is >= 1 and <= 3) continue;   // the pair is one picture, drawn by the lower number
            if (o.Vis && o.Spr != null) q.Add(o.M.Doing == Doing.Surface || o.M.Doing == Doing.Haul || o.M.Doing == Doing.Dive ? _hy + 0.25f * _hry : o.Y, this, o.I);
        }
        q.Add(S.Height + 1f, this, 1001);                                         // droplets and puffs: over everything
    }

    public override void DrawOne(FrameBuffer fb, int index)
    {
        if (_pg == null) return;
        if (index == 1000) { DrawIce(fb); return; }
        if (index == 1001) { DrawBits(fb); return; }
        Otter o = _o[index];
        if (!o.Vis || o.Spr == null) return;
        // On the drift the otter stands ON the snow clump that the depth picture counts as a nearer prop: it must not be hidden by it.
        bool onDrift = o.M.Doing == Doing.Slide && o.Stage >= 6 && o.Stage <= 9;
        if (o.ShadowBin >= 0 && o.ShadowBin < _pg.Shadow.Length)
        {
            Sprite sh = _pg.Shadow[o.ShadowBin];
            if (onDrift) sh.Draw(fb, (int)MathF.Round(o.FX) - sh.Width / 2, (int)MathF.Round(o.FY) - sh.Height / 2, 1f, null, false, S.NearDepth, 254);
            else S.Stamp(fb, sh, o.FX, o.FY, sh.Width / 2, sh.Height / 2);
        }
        if (onDrift)
            o.Spr.DrawSmooth(fb, o.FX - (o.Mirror ? o.Spr.Width - 1 - o.OX : o.OX), o.FY - o.OY, 1f, null, o.Mirror, S.NearDepth, 254);   // at a fraction of a pixel: a slide glides
        else Stamp(fb, o.Spr, o.FX + o.ShakeDX, o.FY, o.OX, o.OY, o.Mirror);
    }
}
















