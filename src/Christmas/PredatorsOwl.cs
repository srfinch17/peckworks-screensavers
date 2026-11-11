using Peckworks.Screensavers.Core;
using Peckworks.Screensavers.Core.Minds;

namespace Christmas;

// ============================================================================
// THE OWL'S VISIT (the logic: Predators.cs has the shared parts and the wolf).
//
// FEYNMAN VERSION. The owl flies at ONE size, in the lane of whatever it is going for (or perches on): a lane is a depth, so an owl that
// stays in its lane never needs a bigger or smaller picture. Its height is a number in metres over that lane's snow: the picture is stamped
// that far above the lane, and a SHADOW (a soft dark ellipse that gets sharper and nearer as it drops) is stamped on the snow below, a little
// to the right (the moon is up and to the left). The gap between a flier and its shadow is what reads as height.
//
// THE OWL'S TRANSITION TABLE. Shared poses: G (the glide: wings level, a slight V), P (perched, upright), and the flap cycle's page 0 and page 12 (both pass
// through G's wing angle, with different hand lag: so the flap and the glide are joined only by the two sheets below).
//   Flap to Glide: FLAP-GLIDE (24 pages: one beat with the amplitude dying away), begun at a flap page 0.   Glide to Flap: GLIDE-FLAP (24 pages: the beat growing), ending on flap page 0.
//   Glide to Stoop: STOOP-IN (24 pages: wings sweep back and half fold, the body pitches down), begun at a glide gate (page 0 or 24: the only two with no bank and no tremor).
//   Stoop-in to Stoop-hold (12 pages, a cycle: the tips tremble) to BRAKE (24 pages, begun at a hold's page 0: the wings thrown wide and back, the tail fanned, the legs out forward,
//   the talons spread), paced by the distance left to the strike.   Brake to Mantle-in (22 pages: the wings sweep forward and down over the spot) to Mantle-idle (24, a cycle:
//   the head bows and bobs) to HEAVE (20 pages: the first deep beat with the prey) to LABOUR (24, a cycle, entered at page 12).
//   Brake to MISS-LAND (28 pages: the wings fold as the body comes upright) to Perch (a cycle) to LOOK-DOWN (24) and SWIVEL (head turned a page per 3 degrees, blended) to HOP (16) to
//   TAKEOFF (24 pages: crouch, spring, the first beat), ending on flap page 12.   Glide to Perch: LAND (30 pages: the flare, then the wings fold), paced by the distance left.
//   Perch to Flap: TAKEOFF.   Interrupts: none (an owl is not afraid of anything here).
// ============================================================================

internal sealed partial class Predators
{
    private const float OFlapV = 4.4f, OGlideV = 4.0f, OTalon = 0.40f;       // metres a second; how far ahead of the body's middle the talons are at the strike

    private void ShowO(int book, int page) { if (_ob == null) return; _o.Bk = _ob[book]; _o.Pg = Math.Clamp(page, 0, _o.Bk.Count - 1); _o.PgF = _o.Pg; }

    private void OSheet(int book, int n, float dur, bool rev, int next) { var o = _o; o.Do = OD.Sheet; o.After = 0; o.SBook = book; o.SN = n; o.SDur = dur; o.ST = 0f; o.SRev = rev; o.Stage = next; ShowO(book, rev ? n - 1 : 0); Log($"OWL sheet {_ob![book].Name}"); }
    private void OLoop(int book, int n, float cycle, float total, int next) { var o = _o; o.Do = OD.Loop; o.SBook = book; o.SN = n; o.SCycle = cycle; o.SLoopFor = total; o.ST = 0f; o.Stage = next; ShowO(book, 0); }

    private bool OwlStart(int kind, bool wantCatch)
    {
        var o = _o; float W = S.Width;
        o.Prey = -1; o.Carry = 0; o.Catch = wantCatch; o.Kind = kind; o.Struck = false; o.SeizeDone = false; o.Cloud = false; o.Swivels = 0;
        PointF lane = default; int dir;
        if (kind is 2 or 3 or 4)
        {
            int best = -1; float bestScore = -1e9f; Cover? bestCov = null; float bestDist = 99f;
            PV[] pv = PreyView(kind);
            if (kind == 3 && _field.HareCount < 3) return false;
            if (kind == 2 && _mice.Count < 2) return false;
            if (kind == 4 && _sq.Count < 2) return false;
            for (int i = 0; i < pv.Length; i++)
            {
                PV v = pv[i];
                if (!Still(v) || v.Feet.Y < _yLo || v.Feet.Y > _field_yHi()) continue;
                if (MathF.Max(v.Feet.X, W - v.Feet.X) / _m < 13.5f) continue;
                if (kind == 4 && PineGap(v.Feet) < 0.16f * S.U) continue;                 // a squirrel is taken only FAR FROM A PINE (near one it would be up the trunk in a heartbeat)
                Cover? cv = NearestCover(v.Feet, out float dm);
                if (kind == 4) dm = 99f;                                                   // a squirrel's cover is a pine, and it is far: no cover to reach
                float score = wantCatch ? dm : -dm;
                if (score > bestScore) { bestScore = score; best = i; bestCov = cv; bestDist = dm; }
            }
            if (best < 0) return false;
            lane = pv[best].Feet;
            // an honest outcome: prey beside cover escapes; prey far from any cover is caught; a miss with no cover near becomes a catch
            o.Catch = wantCatch;
            if (wantCatch && bestDist < 1.8f && _test == "natural") o.Catch = false;
            if (!wantCatch && bestDist > 6.0f) o.Catch = true;
            o.Cov = o.Catch ? null : bestCov;
            if (kind == 4) { o.Catch = true; o.Cov = null; bestDist = 99f; }
            if (o.Catch) PreyStalked(kind, best);
            Log($"OWL picks {(kind == 2 ? "mouse" : kind == 4 ? "squirrel" : "hare")}{best} at {lane.X:F0},{lane.Y:F0}, cover {bestDist:F1} m off, will try for {(o.Catch ? "the catch" : "a miss")}");
            o.Prey = best; o.PX = lane.X; o.PY = lane.Y; o.GY = lane.Y;
            dir = lane.X > 0.5f * W ? 1 : -1;
            if (MathF.Abs(lane.X - (dir > 0 ? 0f : W)) / _m < 13.5f) dir = -dir;
            o.HEnd = 0.44f;
            if (o.Cov is Cover c2)
            {
                // a MISS ends ON TOP of the cover the prey has reached: the talons come down on the rock's crown
                float top = c2.Foot.Y - c2.Height * 0.92f;
                o.PX = c2.Foot.X; o.HEnd = Math.Clamp((o.GY - top) / _m + 0.40f, 0.5f, 2.4f);
            }
            o.Stage = 10; o.FlapFor = 99f;
        }
        else if (kind == 1)
        {
            // a perch: the lamp post's crossbar or the bird table's roof
            int pk = Rng.Next(3); bool lamp = pk == 0;
            PointF at = lamp ? S.LampBar.At : S.FeederRoof.At; PointF foot = lamp ? S.LampFoot : S.FeederFoot;
            if (pk == 2)
            {
                // the crown of a rock or a stump
                var tops = new List<Cover>(); foreach (Cover c in S.Covers) if (CoverOk(c) && c.Kind is CoverKind.Rocks or CoverKind.Stump) tops.Add(c);
                if (tops.Count > 0) { Cover c = tops[Rng.Next(tops.Count)]; at = new PointF(c.Foot.X, c.Foot.Y - c.Height * 0.92f); foot = c.Foot; }
                else { pk = 1; at = S.FeederRoof.At; foot = S.FeederFoot; }
            }
            o.PerchX = at.X; o.GY = foot.Y; o.PerchH = Math.Clamp((foot.Y - (at.Y - 0.30f * _m)) / _m, 0.3f, 6f); o.PerchKind = pk;
            dir = at.X > 0.5f * W ? 1 : -1;
            if (MathF.Abs(at.X - (dir > 0 ? 0f : W)) / _m < 13.5f) dir = -dir;
            o.Stage = 30; o.FlapFor = 99f;
            Log($"OWL will perch on the {(pk == 0 ? "lamp post's crossbar" : pk == 1 ? "bird table's roof" : "crown of a rock")} (height {o.PerchH:F1} m)");
        }
        else
        {
            dir = Rng.Next(2) == 0 ? 1 : -1;
            o.GY = Math.Clamp(S.FieldPoint(0.5f * W, 0.30f + 0.2f * (float)Rng.NextDouble()).Y, _yLo, _field_yHi());
            o.Stage = 40; o.FlapFor = 2.2f + 1.5f * (float)Rng.NextDouble();
        }
        o.Dir = dir; o.On = true; o.Mir = dir < 0;
        o.X = dir > 0 ? -2.2f * _m : W + 2.2f * _m;
        // A HUNT NEEDS A LONG RUN-IN: the flap, the glide, the stoop and the brake each begin only at a set page, and each needs its distance (see
        // StepFlap and StepGlide). So a hunting owl starts at least eighteen and a half metres from its prey, farther off screen if it must: nobody
        // sees the extra second, and the brake (the picture people know) always gets its three metres.
        if (kind >= 2) o.X = dir > 0 ? MathF.Min(o.X, o.PX - 18.5f * _m) : MathF.Max(o.X, o.PX + 18.5f * _m);
        o.H = 3.0f + 0.5f * (float)Rng.NextDouble(); o.HT = o.H;
        o.Pace.Halt(); o.Do = OD.Flap; o.T = 0f; o.PostT = 0f; o.BreathT = 0f; o.LastGate = -1f; o.LastPage = 0; o.WantGlide = false; o.Yaw = 0f;
        ShowO(PredatorsArt.OFlapB, 0);
        Log($"OWL visit begins: kind {(kind == 0 ? "cross" : kind == 1 ? "perch" : kind == 2 ? "hunt a mouse" : kind == 4 ? "hunt a squirrel" : "hunt a hare")}, from the {(dir > 0 ? "left" : "right")}");
        return true;
    }

    private float _field_yHi() => _yHi + 0.05f * S.U;

    private void EndOwl()
    {
        var o = _o;
        PreyStalked(2, -1); PreyStalked(3, -1); PreyStalked(4, -1);
        o.On = false; o.Do = OD.Off; _lastOwlEnd = _time;
        double f = Math.Max(0.2, _pct);
        _nextOwl = _time + (_test == "many" ? 38.0 + 15.0 * Rng.NextDouble() : (120.0 + 120.0 * Rng.NextDouble()) / f);
        Log($"OWL visit ends; the next is not before {_nextOwl:F0} s");
    }

    private bool OOff() => _o.X < -2.6f * _m || _o.X > S.Width + 2.6f * _m;

    /// <summary>One frame of flying: forward at the speed of the gait, the height eased toward the wanted one.</summary>
    private void OFly(float dt, float v)
    {
        var o = _o;
        o.X += o.Dir * v * _m * dt;
        float want = o.HT - o.H;
        o.H += Math.Clamp(want * 1.1f, -1.5f, 1.0f) * dt;
    }

    private void OwlUpdate(float dt)
    {
        var o = _o;
        o.T += dt;
        // the prey's place (a settled mouse or a sitting hare hardly moves: the strike aims at where it is)
        if (o.Prey >= 0 && !o.Struck)
        {
            if (o.Cov == null) { PV v = PreyView(o.Kind)[o.Prey]; if (v.Out) { o.PX = v.Feet.X; o.PY = v.Feet.Y; } }
            o.StrikeX = o.PX - o.Dir * OTalon * _m;
            // THE PREY HAS GONE BEFORE THE OWL COMMITTED (it went home, a story borrowed it): an owl does not dive at bare snow. While it is still
            // cruising (flapping in, or gliding, before the stoop) it simply gives the hunt up and flies on across, like a visit that only crosses.
            if (o.Catch && o.Cov == null && o.Stage is 10 or 12 && o.Do is OD.Flap or OD.Glide && !PreyView(o.Kind)[o.Prey].Out)
            {
                Log("OWL: its prey is no longer in the open; it gives the hunt up and flies on");
                PreyStalked(2, -1); PreyStalked(3, -1); PreyStalked(4, -1);
                o.Prey = -1; o.Kind = 0;
                if (o.Stage == 10) { o.Stage = 40; o.FlapFor = o.T + 1.0f; } else o.Stage = 42;
            }
        }
        // the pins: only when LOW (under 2 m, below the pines' tips), at the shadow's place
        bool hunting = o.Kind >= 2 && o.Stage is >= 13 and <= 26;
        // ...and while it SITS on a perch, at the perch's foot: an owl on a rock is a thing a mouse keeps away from (on film a mouse strolled past
        // half a metre under one).
        bool perched = o.Kind == 1 && o.Stage is >= 34 and <= 37;
        if ((hunting && o.H < 2.0f) || perched) { o.PostT -= dt; if (o.PostT <= 0f) { Board.Post(Sense.Threat, new PointF(o.X + 0.3f * o.H * _m, o.GY), 0.35f * S.U, 0.9f, this); o.PostT = 0.6f; } }
        if (o.Do != OD.Off && o.H < 1.2f && o.Stage is >= 16 and <= 26) S.Occupy(this, 1, o.X, o.GY, 0.5f * _m);
        _field.PredatorIsWolf = false; _field.PredatorAt = (o.Kind >= 2 && o.Stage is >= 13 and <= 26) ? new PointF(o.X, o.GY) : null;

        switch (o.Do)
        {
            case OD.Flap: StepFlap(dt); break;
            case OD.Glide: StepGlide(dt); break;
            case OD.Sheet: StepOSheet(dt); break;
            case OD.Loop: StepOLoop(dt); break;
            case OD.Stoop: StepStoop(dt); break;
            case OD.Brake: StepOBrake(dt); break;
            case OD.Idle: StepSwivel(dt); break;
        }
        if (_log != null && o.Bk != null) _log.WriteLine($"{_time,8:F2} predators owl {o.X:F1} {o.GY:F1} h {o.H:F2} {o.Do} {o.Bk.Name} {o.Pg}");
    }

    // ---- the cruise: flapping, gliding, and the gates between ----
    private void StepFlap(float dt)
    {
        var o = _o;
        float v = OFlapV;
        if (o.Stage is 27) { o.HT = MathF.Min(3.8f, o.H + 1.0f); }
        // the page by time: 24 pages a beat, 0.70 s
        float ph = (o.T / 0.70f); int pg = (int)((ph - MathF.Floor(ph)) * PredatorsArt.NFlap) % PredatorsArt.NFlap;
        if (o.Pg == 0 && false) { }
        bool gate = pg < o.LastPage && o.LastPage > 12;               // page 0 has come round again
        o.LastPage = pg;
        ShowO(PredatorsArt.OFlapB, pg);
        OFly(dt, v);
        if (o.Stage == 27 && OOff()) { EndOwl(); return; }
        if (!gate) return;
        switch (o.Stage)
        {
            case 10:
            {
                float R = (o.StrikeX - o.X) * o.Dir;
                // (16.5 m: a beat is 3.1 m and the change to the glide 2.9 m, so the glide begins 10.5 to 13.6 m out and its gates fall where the stoop wants them)
                if (R <= 16.5f * _m) { o.Stage = 11; o.HT = 3.0f; OSheet(PredatorsArt.OFlapGlide, PredatorsArt.NFlapGlide, 0.70f, false, 12); }
                break;
            }
            case 30:
            {
                float R = (o.PerchX - o.X) * o.Dir;
                if (R <= 13.5f * _m) { o.Stage = 31; o.HT = 2.8f; OSheet(PredatorsArt.OFlapGlide, PredatorsArt.NFlapGlide, 0.70f, false, 32); }
                break;
            }
            case 40:
                if (o.T >= o.FlapFor) { o.HT = 2.4f + 1.4f * (float)Rng.NextDouble(); OSheet(PredatorsArt.OFlapGlide, PredatorsArt.NFlapGlide, 0.70f, false, 42); }
                break;
        }
    }

    private void StepGlide(float dt)
    {
        var o = _o;
        // the glide book: 48 pages at 30 a second; GATES are page 0 and page 24 (no bank, no tremor)
        int pg = (int)(o.T * 30f) % PredatorsArt.NGlide;
        bool gate = (pg < o.LastPage && o.LastPage > 40) || (pg >= 24 && o.LastPage < 24);
        o.LastPage = pg;
        ShowO(PredatorsArt.OGlideB, pg);
        // a glide loses a little height; a bird that wants to climb goes back to flapping at the next gate
        o.HT = MathF.Min(o.HT, o.H);
        OFly(dt, OGlideV);
        if (o.Kind == 0 && OOff()) { EndOwl(); return; }
        if (!gate) return;
        switch (o.Stage)
        {
            case 12:
            {
                float R = (o.StrikeX - o.X) * o.Dir;
                if (R <= 9.4f * _m)
                {
                    // THE STOOP BEGINS: wings sweep back and half fold, the body pitches down. The glide's gates are 3.2 m apart, so this happens 6.2 to
                    // 9.4 m from the strike: the descent then has 3.2 to 6.4 m (0.6 to 1.25 s) before the brake's three metres. (It used to begin as near
                    // as 4.4 m with the fold on a 0.8 s clock: the owl was on top of its prey before the fold ended and the brake flashed by in 3 frames.)
                    o.Stage = 13; o.R0 = R; o.K = 0f; o.H0 = o.H; o.V0 = OGlideV * _m; o.Rb = 3.0f * _m;
                    OSheet(PredatorsArt.OStoopIn, PredatorsArt.NStoopIn, 0.80f, false, 14);
                    o.Do = OD.Sheet; o.After = 1;           // 1: the stoop's descent runs while the sheet plays (see StepOSheet)
                    o.Pace.Halt(); o.Pace.Walked = 0f; o.Pace.Resume(OGlideV * _m);
                    Log($"OWL STOOPS from {o.H:F1} m, {R / _m:F1} m from the strike");
                }
                break;
            }
            case 32:
            {
                float R = (o.PerchX - o.X) * o.Dir;
                if (R <= 6.4f * _m && R >= 2.6f * _m)
                {
                    o.Stage = 33; o.Rb = R; o.H0 = o.H; o.BrakeD = R;
                    o.Pace.Halt(); o.Pace.Walked = 0f; o.Pace.Resume(OGlideV * _m); o.Pace.Brake(R);
                    OSheet(PredatorsArt.OLandB, PredatorsArt.NLand, 99f, false, 34); o.After = 2;   // 2: the landing's pages follow the distance flown (see StepOSheet)
                    Log($"OWL lands: {R / _m:F1} m to the perch");
                }
                break;
            }
            case 40:
                if (o.T >= o.GlideFor) { OSheet(PredatorsArt.OGlideFlap, PredatorsArt.NGlideFlap, 0.70f, false, 41); }
                break;
        }
    }

    // ---- sheets, loops, and what follows each ----
    private void StepOSheet(float dt)
    {
        var o = _o;
        o.ST += dt;
        // a sheet that is part of a movement carries the movement
        if (o.After == 1) { StoopMove(dt); }
        else if (o.After == 2) { LandMove(dt); return; }
        else if (o.After == 3) { MantleMove(dt); }
        else if (o.After == 4) { HeaveMove(dt); }
        else if (o.After == 5) { TakeoffMove(dt); }
        else if (o.After == 6) { float uu = Math.Clamp(o.ST / o.SDur, 0f, 1f); o.H = Lerp(o.H0, o.H0 - 0.10f, PredatorsArt.Sm(uu)); }
        else if (o.Stage is 11 or 31 or 41 or 12 or 32 or 42 or 43 || o.SBook == PredatorsArt.OFlapGlide || o.SBook == PredatorsArt.OGlideFlap) { OFly(dt, Math.Min(OFlapV, Lerp(OFlapV, OGlideV, 0.5f))); }
        // (the stoop-in's pages follow the DISTANCE descended, not a clock: the fold ends exactly where the brake begins, however far out it began)
        float u = o.After == 1 ? o.K : Math.Clamp(o.ST / o.SDur, 0f, 1f);
        int pg = (int)MathF.Round(u * (o.SN - 1)); if (o.SRev) pg = o.SN - 1 - pg;
        ShowO(o.SBook, pg);
        if (o.SBook == PredatorsArt.OMissLand || o.SBook == PredatorsArt.OHopB) { if (o.SBook == PredatorsArt.OHopB) o.X += o.Dir * 0.9f * _m * dt; }
        if (o.After == 1 ? u < 1f : o.ST < o.SDur) return;
        OwlAfterSheet();
    }

    private void OwlAfterSheet()
    {
        var o = _o;
        int nextStage = o.Stage; o.After = 0;
        Log($"OWL sheet ends -> stage {nextStage}");
        switch (nextStage)
        {
            case 12: case 32: case 42: o.Stage = nextStage; o.Do = OD.Glide; o.T = 0f; o.LastPage = 0; o.GlideFor = 2.6f + 2.0f * (float)Rng.NextDouble(); break;
            case 41: o.Stage = 40; o.Do = OD.Flap; o.T = 0f; o.LastPage = 0; o.FlapFor = 2.0f + 1.5f * (float)Rng.NextDouble(); break;
            case 14:
                // the stoop-in ended on stoop pose 0: hold it (a cycle) until the brake can begin at a cycle's page 0
                o.Stage = 14; OLoop(PredatorsArt.OStoopHold, PredatorsArt.NStoopHold, 0.40f, 99f, 15); o.Do = OD.Stoop;
                // the fold ended on the hold's page 0, which is the brake's first picture too: if the strike is near enough the brake begins HERE
                { float R = (o.StrikeX - o.X) * o.Dir; if (R <= 3.6f * _m) BeginOBrake(R); }
                break;
            case 16:
                o.Stage = 17; OLoop(PredatorsArt.OMantleIdle, PredatorsArt.NMantleIdle, 1.3f, 1.55f, 18); break;
            case 18:
                o.Stage = 18; o.Carry = o.Kind == 2 ? 1 : o.Kind == 4 ? 3 : 2; o.F1 = false; o.H0 = o.H; o.Pace.Halt(); o.Pace.Walked = 0f; o.Pace.Resume(0.1f * _m);
                OSheet(PredatorsArt.OHeave, PredatorsArt.NHeave, 0.70f, false, 19); o.After = 4; break;
            case 19:
                o.Stage = 19; o.Do = OD.Loop; o.SBook = PredatorsArt.OLabourB; o.SN = PredatorsArt.NLabour; o.SCycle = o.Kind == 2 ? 0.95f : o.Kind == 4 ? 1.05f : 1.15f; o.SLoopFor = 99f; o.ST = o.SCycle * 0.5f; o.HT = 3.6f;
                break;
            case 21:
                o.Stage = 21; OLoop(PredatorsArt.OPerchB, PredatorsArt.NPerch, 3.0f, 1.4f, 22); break;
            case 22:
                OSheet(PredatorsArt.OLookDown, PredatorsArt.NLookDown, 1.1f, false, 23); break;
            case 23:
                o.Stage = 23; OLoop(PredatorsArt.OPerchB, PredatorsArt.NPerch, 3.0f, 3.0f, 24); break;
            case 25:
                // after the swivel toward the hole: the hop, then the takeoff
                OSheet(PredatorsArt.OHopB, PredatorsArt.NHop, 0.55f, false, 26); break;
            case 26: case 35:
                o.Stage = 27; o.Pace.Halt(); o.Pace.Walked = 0f; o.Pace.Resume(0.1f * _m); o.H0 = o.H;
                OSheet(PredatorsArt.OTakeoff, PredatorsArt.NTakeoff, 0.80f, false, 27); o.After = 5; break;
            case 27:
                o.Do = OD.Flap; o.T = 0.35f; o.LastPage = 12; o.Stage = 27; break;
            case 34:
                o.Stage = 34; o.Swivels = 0; OLoop(PredatorsArt.OPerchB, PredatorsArt.NPerch, 3.0f, 2.2f + 1.5f * (float)Rng.NextDouble(), 36); break;
            default: EndOwl(); break;
        }
    }

    private void StepOLoop(float dt)
    {
        var o = _o;
        o.ST += dt;
        int pg = (int)(o.ST / o.SCycle * o.SN) % o.SN;
        bool gate = pg < o.LastPage && o.LastPage > o.SN / 2; o.LastPage = pg;
        ShowO(o.SBook, pg);
        o.BreathT -= dt;
        if (o.Stage == 19)
        {
            // THE LABOUR: climbing out over the pines, slower and lower than a free owl, a heavy beat; off the top or the side
            o.HT = 3.9f;
            OFly(dt, o.Kind == 2 ? 3.2f : o.Kind == 4 ? 2.8f : 2.5f);
            o.H += 0.0f;
            if (OOff() || o.H > 3.7f && (o.X < -0.5f * _m || o.X > S.Width + 0.5f * _m)) { EndOwl(); return; }
            return;
        }
        if (o.ST < o.SLoopFor) return;
        switch (o.Stage)
        {
            case 18: case 22: OwlAfterSheet(); break;                        // the mantle ends in the heave; the ground perch ends in the look down
            case 24: case 36: if (gate) BeginSwivel(); break;                // a perch's idle ends at its page 0 (the swivel's first page is the same picture)
        }
    }

    // ---- the swivel: the head turns far round, slowly, with stops ----
    private void BeginSwivel()
    {
        var o = _o;
        o.Do = OD.Idle; o.Stage = o.Stage is 23 or 24 ? 24 : 37; o.YawT = 0f; o.YawFrom = 0f;
        float toward = o.Kind >= 2 && !o.Catch ? 80f : 70f + 80f * (float)Rng.NextDouble();
        if (Rng.Next(2) == 0 && !(o.Kind >= 2)) toward = 180f - 40f * (float)Rng.NextDouble();
        o.YawTo = toward; o.YawDur = toward / 55f + 0.4f; o.Hold = 1.2f + 1.6f * (float)Rng.NextDouble(); o.Swivels++;
        o.ST = 0f;
        Log($"OWL swivels its head to {toward:F0} degrees");
    }

    private void StepSwivel(float dt)
    {
        var o = _o;
        o.YawT += dt;
        float total = o.YawDur * 2f + o.Hold;
        float yaw;
        if (o.YawT < o.YawDur) yaw = o.YawTo * PredatorsArt.Sm(o.YawT / o.YawDur);
        else if (o.YawT < o.YawDur + o.Hold) yaw = o.YawTo + 3f * MathF.Sin((o.YawT - o.YawDur) * 2.2f);
        else yaw = o.YawTo * (1f - PredatorsArt.Sm((o.YawT - o.YawDur - o.Hold) / o.YawDur));
        o.Yaw = Math.Clamp(yaw, 0f, 180f);
        ShowO(PredatorsArt.OSwivelB, (int)(o.Yaw / 3f)); o.PgF = o.Yaw / 3f;
        if (o.YawT >= total)
        {
            o.Yaw = 0f; o.PgF = 0f;
            bool ground = o.Kind >= 2;
            if (ground) { o.Stage = 25; OwlAfterSheet(); }                       // after the head turn toward the hole: the hop and the takeoff
            else if (o.Swivels < 2) { o.Stage = 34; OLoop(PredatorsArt.OPerchB, PredatorsArt.NPerch, 3.0f, 1.2f, 36); }
            else { o.Stage = 35; OwlAfterSheet(); }
        }
    }

    // ---- the stoop and the brake ----
    private void StoopMove(float dt)
    {
        var o = _o;
        float R = (o.StrikeX - o.X) * o.Dir;
        float k = Math.Clamp(1f - (R - o.Rb) / MathF.Max(1f, o.R0 - o.Rb), 0f, 1f);       // 0 at the start, 1 where the brake begins
        k = MathF.Max(k, o.K); o.K = k;                                                    // (never backwards: a prey that shuffles away does not unfold the wings)
        o.Pace.Want = Lerp(o.V0, 6.2f * _m, PredatorsArt.Sm(k)); o.Pace.Accel = 6f * _m;
        float step = o.Pace.Tick(dt); o.X += o.Dir * step;
        float Hb = MathF.Max(1.15f, o.HEnd + 0.35f);
        o.H = Hb + (o.H0 - Hb) * MathF.Pow(1f - k, 1.8f);
    }

    private void StepStoop(float dt)
    {
        // the stoop hold: a cycle of 12 pages; the brake begins at a cycle's page 0 once the strike is near enough
        var o = _o;
        StoopMove(dt);
        o.ST += dt;
        int pg = (int)(o.ST / o.SCycle * o.SN) % o.SN;
        bool gate = pg < o.LastPage && o.LastPage > 6; o.LastPage = pg;
        ShowO(PredatorsArt.OStoopHold, pg);
        float R = (o.StrikeX - o.X) * o.Dir;
        if ((gate && R <= 3.6f * _m) || R <= 1.4f * _m) BeginOBrake(R);
    }

    /// <summary>THE BRAKE begins, R pixels from the strike: paced by the distance left (the speed falls to nothing exactly at the strike point).</summary>
    private void BeginOBrake(float R)
    {
        var o = _o;
        o.Stage = 15; o.Do = OD.Brake; o.T = 0f; o.BrakeD = MathF.Max(1.0f * _m, R); o.H0 = o.H; o.Pace.Brake(o.BrakeD); o.ST = 0f;
        Log($"OWL BRAKES: {o.BrakeD / _m:F1} m to the strike from {o.H:F2} m up");
    }

    private void StepOBrake(float dt)
    {
        var o = _o;
        float step = o.Pace.Tick(dt); o.X += o.Dir * step;
        float done = o.BrakeD > 0 ? 1f - Math.Clamp((o.StrikeX - o.X) * o.Dir / o.BrakeD, 0f, 1f) : 1f;
        float u = Math.Clamp(done, 0f, 1f);
        ShowO(PredatorsArt.OBrakeB, (int)MathF.Round(u * (PredatorsArt.NBrake - 1)));
        o.H = Lerp(o.H0, o.HEnd, PredatorsArt.Sm(u));
        // the cloud of powder begins as the talons come down (dense before the prey is taken)
        if (!o.Cloud && u > 0.80f && o.Cov == null) { o.Cloud = true; StartCloud(o.PX, o.PY, 0.34f); SnowBurst(o.PX, o.PY, 7, 0.7f, 0.9f); }
        if (!o.Pace.Braking || u >= 0.995f)
        {
            o.Struck = true; o.Pace.Halt();
            bool took = false;
            if (o.Prey >= 0 && o.Catch && o.Cov == null)
            {
                PV pvv = PreyView(o.Kind)[o.Prey];
                float d = MathF.Abs(pvv.Feet.X - o.PX);
                took = pvv.Out && d < (o.Kind == 2 ? 0.9f : 1.2f) * _m && PreySeize(o.Kind, o.Prey);
                if (took) PreyRestore(o.Kind, o.Prey, 60f + 60f * (float)Rng.NextDouble());
            }
            Log($"OWL STRIKES at {o.PX:F0},{o.PY:F0}: {(took ? "it has the " + (o.Kind == 2 ? "mouse" : o.Kind == 4 ? "squirrel" : "hare") : "it grips empty snow")}");
            PreyStalked(2, -1); PreyStalked(3, -1); PreyStalked(4, -1);
            o.H = o.HEnd;
            if (took) { o.Stage = 16; OSheet(PredatorsArt.OMantleIn, PredatorsArt.NMantleIn, 0.75f, false, 16); o.After = 3; }
            else { o.Stage = 20; o.H0 = o.H; OSheet(PredatorsArt.OMissLand, PredatorsArt.NMissLand, 0.95f, false, 21); o.After = 6; }
        }
    }

    private void LandMove(float dt)
    {
        var o = _o;
        float step = o.Pace.Tick(dt); o.X += o.Dir * step;
        float u = o.BrakeD > 0 ? 1f - Math.Clamp((o.PerchX - o.X) * o.Dir / o.BrakeD, 0f, 1f) : 1f;
        ShowO(PredatorsArt.OLandB, (int)MathF.Round(Math.Clamp(u, 0f, 1f) * (PredatorsArt.NLand - 1)));
        o.H = Lerp(o.H0, o.PerchH, PredatorsArt.Sm(Math.Clamp(u * 1.15f, 0f, 1f)));
        if (!o.Pace.Braking || u >= 0.995f) { o.X = o.PerchX; o.H = o.PerchH; o.Pace.Halt(); o.ST = 0f; o.SDur = 0.001f; o.After = 0; o.Stage = 34; }
        else o.ST = 0f;                       // the sheet's own clock never runs out: the distance ends it
    }

    private void MantleMove(float dt) { var o = _o; o.H = Lerp(o.H, 0.34f, 0.15f); }
    private void HeaveMove(float dt)
    {
        var o = _o;
        float u = Math.Clamp(o.ST / o.SDur, 0f, 1f);
        o.Pace.Want = 1.6f * _m; o.Pace.Accel = 3f * _m;
        float step = o.Pace.Tick(dt); o.X += o.Dir * step;
        // (slow off the ground, then away: ONE smooth curve. It was "half the climb until halfway, then all of it": a step of 0.22 m, 17 pixels, in a
        // single frame, which the pop detector found at the middle of every lift-off.)
        float s = PredatorsArt.Sm(u);
        o.H = o.H0 + 0.9f * s * (0.5f + 0.5f * s);
        if (u > 0.25f && !o.F1) { o.F1 = true; SnowBurst(o.X, o.GY, 8, 0.8f, 0.9f); }
    }
    private void TakeoffMove(float dt)
    {
        var o = _o;
        float u = Math.Clamp(o.ST / o.SDur, 0f, 1f);
        o.Pace.Want = 3.2f * _m; o.Pace.Accel = 4f * _m;
        float step = o.Pace.Tick(dt); o.X += o.Dir * step;
        o.H = o.H0 + 0.7f * PredatorsArt.Sm(u) * u;
    }

    // ================================================================ DRAW

    private void DrawOwlShadow(FrameBuffer fb)
    {
        var o = _o;
        if (o.Bk == null || _shadow == null) return;
        float k = Math.Clamp(o.H / 3.2f, 0f, 1f), pos = k * 2f;
        int i0 = Math.Min(1, (int)pos); float f = pos - i0;
        float sx = o.X + 0.3f * o.H * _m, sy = o.GY + 0.02f * _m;
        float baseOp = 0.95f - 0.40f * k;
        byte near = S.Nearness(o.GY);
        _shadow[i0].DrawCentered(fb, sx, sy, baseOp * (1f - f), null, false, S.NearDepth, near);
        if (f > 0.01f) _shadow[i0 + 1].DrawCentered(fb, sx, sy, baseOp * f, null, false, S.NearDepth, near);
    }

    private void DrawOwl(FrameBuffer fb)
    {
        var o = _o;
        if (o.Bk == null || _ob == null) return;
        float bodyY = o.GY - o.H * _m;
        float feetY = bodyY + PredatorsArt.OwlBase * _m;
        byte near = S.Nearness(o.GY);
        int ox = o.Bk.Ox[0], oy = o.Bk.Oy[0];
        float left = o.X - (o.Mir ? o.Bk.Pages[0][0].Width - 1 - ox : ox), top = feetY - oy;
        if (o.Carry > 0)
        {
            // the prey, hung from the talons (before the owl, so the toes are in front of it)
            PointF a = o.Bk.Aux[o.Pg];
            Book pb = _ob[o.Carry == 1 ? PredatorsArt.OPreyMouse : o.Carry == 3 ? PredatorsArt.OPreySquirrel : PredatorsArt.OPreyHare];
            int sway = Math.Clamp(3 + (int)MathF.Round(3f * MathF.Sin((float)_time * 4.2f)), 0, PredatorsArt.NPrey - 1);
            float px = o.X + (o.Mir ? -a.X : a.X) * _m, py = bodyY - a.Y * _m;
            pb.Pages[0][sway].DrawSmooth(fb, px - pb.Ox[0], py + 0.70f * _m - pb.Oy[0], 1f, null, false, S.NearDepth, near);
        }
        if (o.Do == OD.Idle && o.Bk == _ob[PredatorsArt.OSwivelB])
        {
            int p0 = Math.Clamp((int)MathF.Floor(o.PgF), 0, o.Bk.Count - 1), p1 = Math.Min(o.Bk.Count - 1, p0 + 1);
            Sprite.DrawBlend(fb, o.Bk.Pages[0][p0], left, top, o.Bk.Pages[0][p1], left, top, o.PgF - p0, o.Mir, S.NearDepth, near);
        }
        else o.Bk.Pages[0][o.Pg].DrawSmooth(fb, left, top, 1f, null, o.Mir, S.NearDepth, near);
    }
}

