using Peckworks.Screensavers.Core;
using Peckworks.Screensavers.Core.Minds;

namespace SakuraPond;

/// <summary>
/// The pond snails (round forty-seven, his ask: "add another creature to the
/// water ... maybe snails? maybe you get creative"): a few tanishi, the common
/// river snail of a Japanese pond, creeping over the bed. A coiled brown shell
/// about 0.012u across with a lighter whorl line winding round it, a soft
/// grey-tan foot showing ahead of the shell with two short tentacles that wave
/// as it goes, and a little of the foot trailing behind. They go at a snail's
/// pace: about a body length every twelve seconds, with long stops to graze, so
/// one is noticed only by someone who looks, and then it is seen to be moving.
///
/// WHAT IT IS SOLID TO AND WHAT IT PASSES (his world law, "nothing can clip
/// through anything in this universe"; declared before it was built):
///   * It RIDES THE BED: its depth is the bed's under it (Pond.Floor, the one
///     terrain every swimmer reads), so it climbs a submerged stone's mound and
///     crawls down the far side, and it is drawn sunk, hazed, shrunk and squashed
///     by that depth exactly as a prawn or a crayfish is (Body, Light).
///   * The land, a standing rock, the island's dry cap, a pad's foot and the
///     picture's edge are WALLS: a step onto one is refused and it turns (the
///     prawns' bed test, PrawnBed.Free, with the depth limit lifted).
///   * EVERYTHING ELSE PASSES OVER IT. It is drawn under every swimmer and every
///     bed animal, nothing steers round it and nothing is pushed by it (a crayfish
///     walks straight over a snail, which is what a crayfish does). A stone that
///     stands nearer the eye hides that bit of it (Pond.Near).
///   * Nothing eats it yet (it is not on the Snatch kit); the koi nose past.
///
/// Drawn before the crayfish, so under all of them; its own dice (nothing else in
/// the pond rolls differently for its being here); the "Snails" knob, 0 is none.
/// The instrument log (PECKWORKS_SNAILLOG) is permanent: one line per snail per
/// frame while it is live.
/// </summary>
internal sealed class Snails
{
    public static Action<string>? MotionLog;
    private readonly Snail[] _all;
    private double _t;
    private long _frame;

    /// <param name="count">The "Snails" knob. 0 = none, and every call returns at once.</param>
    /// <param name="waterInView">Open water that no branch hides: where a snail may be drawn and creep.</param>
    /// <param name="rng">The snails' OWN dice (seeded from the scene with a salt of their own), so adding them moved nothing else.</param>
    public Snails(Pond pond, int count, bool[] waterInView, Random rng)
    {
        if (count <= 0) { _all = []; return; }
        var bed = new PrawnBed(pond, waterInView);
        var homes = new List<PointF>();
        // A home each: open, visible bed with a snail's width of free bed all round it, at least 0.1u from every other
        // snail's. THE SHALLOWS FIRST (the first count: four placed, three of them on the deepest bed, where a snail is drawn
        // at 0.44 of its size behind six tenths of haze, five pixels at 1080p; none could be found in its own crop): the first
        // three thousand tries take only bed under 0.3 of the pond's depth (the shore's slope and the stones' mounds, where a
        // prawn lives too), the last thousand take any bed, so a seed with little shallow bed still gets its snails.
        for (int tries = 0; tries < 4000 && homes.Count < count; tries++)
        {
            float sx = (float)(rng.NextDouble() * pond.Width), sy = (float)(rng.NextDouble() * pond.Height);
            (float gx, float gy) = bed.ToGround(sx, sy);
            if (tries < 3000 && bed.ZAt(gx, gy) > 0.3f) continue;
            if (!Snail.Free(bed, gx, gy)) continue;
            bool ringOk = true;
            for (int k = 0; k < 6 && ringOk; k++)
                ringOk = Snail.Free(bed, gx + MathF.Cos(k * MathF.Tau / 6) * bed.U * 0.012f, gy + MathF.Sin(k * MathF.Tau / 6) * bed.U * 0.012f);
            if (!ringOk) continue;
            bool apart = true;
            foreach (PointF h in homes)
                if ((h.X - gx) * (h.X - gx) + (h.Y - gy) * (h.Y - gy) < bed.U * bed.U * 0.1f * 0.1f) { apart = false; break; }
            if (apart) homes.Add(new PointF(gx, gy));
        }
        _all = new Snail[homes.Count];
        for (int i = 0; i < _all.Length; i++) _all[i] = new Snail(bed, i, homes[i], rng);
        string? file = Environment.GetEnvironmentVariable("PECKWORKS_SNAILLOG");
        if (!string.IsNullOrEmpty(file) && MotionLog == null)
        {
            var writer = new StreamWriter(file) { AutoFlush = false };
            writer.WriteLine("# F,t,idx,doing,x,y,z,heading,speed,inView,refused,drawnX,drawnY");
            writer.WriteLine($"# snails {_all.Length} of {count}");
            MotionLog = line => writer.WriteLine(line);
            AppDomain.CurrentDomain.ProcessExit += (_, _) => writer.Flush();
        }
    }

    /// <summary>How many were placed (the start log: a seed with no free bed gets fewer).</summary>
    public int Count => _all.Length;

    public void Update(float dt)
    {
        if (_all.Length == 0) return;
        _t += dt;
        _frame++;
        for (int i = 0; i < _all.Length; i++)
        {
            _all[i].Update(dt);
            if (MotionLog != null) _all[i].Log(_frame, _t, MotionLog);
        }
    }

    /// <summary>On the bed: before every bed animal and every fish (everything else is above it).</summary>
    public void Draw(FrameBuffer fb)
    {
        for (int i = 0; i < _all.Length; i++) _all[i].Draw(fb);
    }
}

/// <summary>
/// One snail. The body is a short spine (Body) along the FOOT, nose first, with
/// the shell a disc painted over its middle in Colour: so the foot and the shell
/// bend, sink, haze and shrink together through the one painter every bed animal
/// uses. The shell is coloured by where a pixel lies round the shell's centre (a
/// whorl line winding out from the apex), which is why it is in Colour and not a
/// stamp: a stamp is one picture turned, and the shell's light must stay on the
/// sun's side as the snail turns.
///
/// WHAT IT DOES: CREEPS (a slow glide, a gentle drift of heading) and GRAZES
/// (stops, the tentacles still waving, for ten to forty seconds). A refused step
/// (a wall) turns it: a snail does not plan, it turns and tries again.
/// </summary>
internal sealed class Snail : Body
{
    private const int Points = 5;
    private enum Doing { Creep, Graze }

    private readonly PrawnBed _bed;
    private readonly Pond _pond;
    private readonly bool[] _view;
    private readonly Random _rng;
    private readonly int _idx;
    private readonly float _u, _L;                   // u, and the foot's full length on the ground at the surface
    private readonly float[] _profile = [0.35f, 0.5f, 1f, 1f, 0.45f];   // half width at each spine point, as a share of the shell's radius
    private readonly Mind<Doing> _mind = new();
    private Doing _doing => _mind.Doing;
    private float _x, _y, _heading, _speed, _z, _sc, _len, _sinkPx, _drift, _wag, _refusedT;
    private bool _refused;

    private const float ShellK = 0.55f;             // where along the foot (nose 0) the shell's centre sits
    private const float ShellR = 0.27f;             // the shell's radius as a share of the foot's length (0.022u long, 0.012u across)

    public Snail(PrawnBed bed, int idx, PointF home, Random rng) : base(Points)
    {
        _bed = bed; _pond = bed.Pond; _view = bed.View; _rng = rng; _idx = idx;
        _u = bed.U;
        _L = _u * 0.022f;
        for (int i = 0; i < Points; i++) Along[i] = i / (Points - 1f);
        _x = home.X; _y = home.Y;
        _heading = (float)(rng.NextDouble() * MathF.Tau) - MathF.PI;
        _wag = (float)(rng.NextDouble() * MathF.Tau);
        _mind.Begin(Doing.Graze, 2 + 30 * (float)rng.NextDouble());   // each starts part way through a graze of its own, so no two set off together
        _z = bed.ZAt(_x, _y);
        LaySpine();
    }

    /// <summary>May a snail's middle be at this ground point? The prawns' bed test with the depth limit lifted to six tenths of the pond's depth (open visible water, bed in sight, not a wall): deeper than that a snail is drawn at half its size behind a third of haze, and in a 600 s run every snail that could wander there did, and was lost to the eye (seed 5, z to 0.90).</summary>
    public static bool Free(PrawnBed bed, float gx, float gy) => bed.Free(gx, gy, deepestU: Pond.DepthU * 0.6f);

    public void Update(float dt)
    {
        _mind.Tick(dt);
        _wag = (_wag + dt * 1.1f) % MathF.Tau;
        if (_mind.Over)
        {
            if (_doing == Doing.Graze) { _mind.Begin(Doing.Creep, 15 + 45 * (float)_rng.NextDouble()); _drift = ((float)_rng.NextDouble() - 0.5f) * 0.12f; }
            else _mind.Begin(Doing.Graze, 10 + 30 * (float)_rng.NextDouble());
        }
        // A snail's pace: a body length every twelve seconds, eased in and out of a graze (nothing it does is sudden).
        float want = _doing == Doing.Creep ? _L / 12f : 0;
        _speed += (want - _speed) * (1 - MathF.Exp(-dt * 0.8f));
        _heading = Wrap(_heading + _drift * dt);
        float step = _speed * dt;
        float nx = _x + MathF.Cos(_heading) * step, ny = _y + MathF.Sin(_heading) * step;
        // The step: the middle of the shell must land on free bed, and so must the nose (the foot is half the shell's width, and a
        // nose on the land with the shell in the water would be a snail half in a wall).
        float noseX = nx + MathF.Cos(_heading) * _L * ShellK, noseY = ny + MathF.Sin(_heading) * _L * ShellK;
        _refused = step > 0 && !(Free(_bed, nx, ny) && Free(_bed, noseX, noseY));
        if (_refused)
        {
            // Turned by a quarter to a half turn, the side rolled, and the creep goes on: a snail at a stone does not plan a way round,
            // it turns and tries again. Refused for three seconds running (a nook) it turns right round.
            _refusedT += dt;
            float turn = _refusedT > 3f ? MathF.PI : (0.5f + 1.0f * (float)_rng.NextDouble()) * MathF.PI * 0.5f * (_rng.Next(2) == 0 ? 1 : -1);
            _heading = Wrap(_heading + turn);
            if (_refusedT > 3f) _refusedT = 0;
        }
        else { _refusedT = 0; _x = nx; _y = ny; }
        _z = _bed.ZAt(_x, _y);                         // it rides the bed: up a mound, down the other side
        LaySpine();
    }

    private void LaySpine()
    {
        _sc = Light.Scale(_z);
        _len = _L * _sc;
        _sinkPx = Light.SinkPx(_z, _u);
        float c = MathF.Cos(_heading), s = MathF.Sin(_heading);
        for (int i = 0; i < Points; i++)
        {
            // The nose is ahead of the middle by ShellK of the length; the spine runs back from it.
            float along = (ShellK - Along[i]) * _len;
            float gx = _x + c * along, gy = _y + s * along;
            float hw = _len * ShellR * _profile[i];
            PointF m = _bed.ToScreen(gx, gy), l = _bed.ToScreen(gx - s * hw, gy + c * hw), r = _bed.ToScreen(gx + s * hw, gy - c * hw);
            Spine[i] = new PointF(m.X, m.Y + _sinkPx);
            HalfWidth[i] = MathF.Max(0.7f, MathF.Sqrt((l.X - r.X) * (l.X - r.X) + (l.Y - r.Y) * (l.Y - r.Y)) / 2);
        }
    }

    /// <summary>Half width at s, as the painter interpolates it (so the shell's disc can be found from s and v).</summary>
    private float HalfAt(float s)
    {
        float f = s * (Points - 1);
        int i = Math.Clamp((int)f, 0, Points - 2);
        return HalfWidth[i] + (HalfWidth[i + 1] - HalfWidth[i]) * (f - i);
    }

    protected override bool Colour(float s, float v, out int r, out int g, out int b, out float alpha)
    {
        // Where this pixel lies round the shell's centre, in the body's own pixels (along the foot, across it).
        float along = (s - ShellK) * _len, across = v * HalfAt(s);
        float R = _len * ShellR;
        float q = (along * along + across * across) / (R * R);
        if (q < 1f)
        {
            // THE SHELL: dark olive-brown, a whorl line (paler, the ridge between the turns) winding out from an apex set a little
            // behind the centre, the sun's side (up and to the left) lighter, the far side darker, and a faint pale rim at the lip.
            float th = MathF.Atan2(across, along - R * 0.15f);
            float rad = MathF.Sqrt(q);
            float whorl = rad * 2.3f + th / MathF.Tau; whorl -= MathF.Floor(whorl);
            float ridge = whorl < 0.14f ? 1 - whorl / 0.14f : 0;
            float lit = Math.Clamp(0.5f - (along * 0.6f + across * 0.8f) / R * 0.45f, 0.1f, 1f);
            r = (int)((66 + 44 * lit) * (1 - 0.35f * ridge) + 40 * ridge);
            g = (int)((50 + 34 * lit) * (1 - 0.35f * ridge) + 32 * ridge);
            b = (int)((28 + 22 * lit) * (1 - 0.35f * ridge) + 18 * ridge);
            alpha = q > 0.86f ? 0.85f : 1f;
            return true;
        }
        // THE FOOT: soft grey-tan, see-through at its edge, the head end a shade darker.
        float av = MathF.Abs(v);
        r = 122 - (int)(14 * (1 - s)); g = 108 - (int)(12 * (1 - s)); b = 84 - (int)(8 * (1 - s));
        alpha = 0.72f * Math.Clamp((1 - av) * 2.5f, 0, 1);
        return alpha > 0.02f;
    }

    public void Draw(FrameBuffer fb)
    {
        float murk = Light.Murk(_z);
        if (_len < 5f) return;                        // the preview box: too small to be anything but a dot
        float soft = MathF.Max(0.8f, _u * 0.0006f);
        // The shadow on the bed: a hair down and to the right (it sits ON the bed, so the gap is the least of any animal's).
        float reach = _u * 0.004f;
        PaintShadow(fb, _view, reach * 0.6f, reach * 0.8f, 0.25f * (1 - murk), soft * 2f, near: _pond.Near, nearness: Light.Nearness(_z));
        Paint(fb, _view, _pond.Sun, murk, soft, _pond.Pixels, near: _pond.Near, nearness: Light.Nearness(_z));
        // Two short tentacles from the head, each waving slowly on its own beat, drawn as the water would show them: the foot's own tone, deepened.
        PointF head = Spine[0];
        float c = MathF.Cos(_heading), s = MathF.Sin(_heading);
        float tl = _len * 0.3f;
        int tr = 112, tg = 98, tb = 76;
        Deepen(ref tr, ref tg, ref tb, (int)(murk * 256));
        var tone = Color.FromArgb(tr, tg, tb);
        for (int side = -1; side <= 1; side += 2)
        {
            float a = _heading + side * (0.6f + 0.18f * MathF.Sin(_wag + (side > 0 ? 0 : 2.1f)));
            var tip = new PointF(head.X + MathF.Cos(a) * tl, head.Y + MathF.Sin(a) * tl * Ripples.Squash);
            fb.Line(head, tip, tone, 0.7f * (1 - murk * 0.6f), MathF.Max(0.8f, _len * 0.045f), _view, _pond.Near, Light.Nearness(_z));
        }
    }

    public void Log(long frame, double t, Action<string> log)
    {
        int ix = (int)Spine[2].X, iy = (int)Spine[2].Y;
        bool inView = ix >= 0 && iy >= 0 && ix < _pond.Width && iy < _pond.Height && _view[iy * _pond.Width + ix];
        log(FormattableString.Invariant($"F,{t:F3},{_idx},{_doing},{_x:F2},{_y:F2},{_z:F4},{_heading:F3},{_speed:F3},{(inView ? 1 : 0)},{(_refused ? 1 : 0)},{Spine[2].X:F1},{Spine[2].Y:F1}"));
    }

    private static float Wrap(float a) { while (a > MathF.PI) a -= MathF.Tau; while (a < -MathF.PI) a += MathF.Tau; return a; }
}
