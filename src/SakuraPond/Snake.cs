using Peckworks.Screensavers.Core;

namespace SakuraPond;

/// <summary>
/// Now and then a snake crosses: a shimahebi (the Japanese four-lined rat
/// snake, olive with four dark stripes running down its length), a good
/// swimmer and a common sight at ponds. It comes out of the grass on one
/// side, swims across the pond and slips away out of the other side.
///
/// HOW A SNAKE SWIMS: its head weaves side to side as it goes forward, and
/// every part of its body passes through exactly the same points the head
/// did, like a line of people following a leader through a winding path.
/// So we move only the head (forward along its heading, weaving), remember
/// where it has been, and lay the body along that trail (Body.cs draws it
/// bending). Its head pushes a small V-shaped wake out across the water.
///
/// HOW IT STEERS: it is not on rails. It has somewhere it wants to get to
/// (a point beyond the far side of the screen) and it feels its way there,
/// a little like the koi: each frame it looks a body length ahead, and if a
/// lily pad, a rock, the log, the cattails or an iris clump is in the way it
/// tries a few headings either side and takes the smallest turn that clears
/// the thing, then settles back toward its goal. The body follows the head,
/// so the whole snake flows round the pad rather than cutting across it.
/// On land the same rule keeps it off the rocks and the log.
///
/// AND IT IS NERVOUS: if a koi, a turtle or the lizard comes close enough to
/// touch any part of it, it startles: a burst of speed, a swing away from
/// whatever touched it, the weave quickening with the speed, rings thrown
/// off its head, then over a few seconds it calms back to its unhurried
/// crossing. It will not startle again for a while, so a koi that keeps
/// pace with it does not set it off thirty times a second.
/// </summary>
internal sealed class Snake : Body
{
    private const int Points = 44;
    private const float FleeFor = 2.6f;                   // seconds from a startle back to calm

    private readonly Pond _pond;
    private readonly Ripples _ripples;
    private readonly Random _rng;
    private readonly float _u, _often, _len, _wide;
    private readonly PointF[] _trail = new PointF[400];
    private int _trailHead, _trailCount;

    // Everything on the pond it must go round, as circles (lily pads, rocks,
    // the cattail clump, the iris clumps) plus the log as a thick line.
    private readonly PointF[] _obsCentre;
    private readonly float[] _obsRadius;
    private readonly Log? _log;

    // Where its head is (screen pixels, before the weave is added), which
    // way it is going, how fast, and how far it has come (the weave's clock).
    private float _x, _y, _heading, _speed, _travelled;
    private PointF _goal;                                  // the point beyond the far edge it is making for
    private float _wakeTimer, _next, _alive;
    private float _flee, _fleeAway, _calm;                 // startle: seconds left, the way away, seconds before it can startle again
    private bool _live, _blocked;                          // _blocked: last frame's step was refused

    public Snake(Pond pond, Ripples ripples, float often, Random rng) : base(Points)
    {
        _pond = pond;
        _ripples = ripples;
        _rng = rng;
        _u = pond.U;
        _often = often;
        _len = _u * 0.3f;
        _wide = _u * 0.0068f;
        for (int i = 0; i < Points; i++) Along[i] = i / (Points - 1f);
        _next = 25 + 20 * (float)rng.NextDouble();       // the first crossing comes within a minute

        var centres = new List<PointF>();
        var radii = new List<float>();
        foreach (LilyPad p in pond.Pads) { centres.Add(p.Center); radii.Add(p.Radius); }
        foreach (BankRock r in pond.Rocks) { centres.Add(r.Centre); radii.Add(r.Radius); }
        centres.Add(pond.CattailSpot); radii.Add(_u * 0.06f);
        foreach ((PointF root, _) in pond.IrisSpots) { centres.Add(root); radii.Add(_u * 0.045f); }
        _obsCentre = centres.ToArray();
        _obsRadius = radii.ToArray();
        _log = pond.Log;
    }

    /// <summary>Is it on screen (or coming or going) right now?</summary>
    public bool Live => _live;

    /// <summary>Its head, screen pixels (the minnows keep clear of it).</summary>
    public PointF Head => Spine[0];

    /// <summary>
    /// Plans a crossing: from beyond one edge of the screen toward a point
    /// well beyond the far side. The route itself is found on the way.
    /// </summary>
    private void Begin()
    {
        float a = (float)(_rng.NextDouble() * MathF.Tau), b = a + MathF.PI + ((float)_rng.NextDouble() - 0.5f) * 1.2f;
        PointF from = OffScreen(a), to = OffScreen(b);
        // The goal sits a good way past the far edge, so the snake is well
        // out of sight (and over) before it could ever reach it and turn back.
        _goal = new PointF(to.X + MathF.Cos(b) * _u * 0.6f, to.Y + MathF.Sin(b) * _u * 0.6f);
        _x = from.X; _y = from.Y;
        _heading = MathF.Atan2(to.Y - from.Y, to.X - from.X);
        _speed = _u * 0.06f;                              // an unhurried swim: a screen height in about fifteen seconds
        _travelled = 0; _alive = 0; _flee = 0; _calm = 0; _blocked = false;
        _trailCount = 0;
        // Its body starts laid out straight behind it, off the screen.
        for (int i = 60; i >= 0; i--) Remember(new PointF(_x - MathF.Cos(_heading) * _len * 1.2f * i / 60, _y - MathF.Sin(_heading) * _len * 1.2f * i / 60));
        _live = true;
    }

    /// <summary>A point beyond the screen's edge in direction a from the pond's middle.</summary>
    private PointF OffScreen(float a)
    {
        PointF c = _pond.Centre;
        float dx = MathF.Cos(a), dy = MathF.Sin(a), margin = _u * 0.08f;
        float tx = dx > 0 ? (_pond.Width + margin - c.X) / dx : dx < 0 ? (-margin - c.X) / dx : float.MaxValue;
        float ty = dy > 0 ? (_pond.Height + margin - c.Y) / dy : dy < 0 ? (-margin - c.Y) / dy : float.MaxValue;
        float t = MathF.Min(tx, ty);
        return new PointF(c.X + dx * t, c.Y + dy * t);
    }

    private static float Dist(PointF a, PointF b) => MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
    private static float Wrap(float a) => MathF.IEEERemainder(a, MathF.Tau);

    private void Remember(PointF p)
    {
        _trailHead = (_trailHead + 1) % _trail.Length;
        _trail[_trailHead] = p;
        _trailCount = Math.Min(_trail.Length, _trailCount + 1);
    }

    /// <summary>
    /// How much room there is at a point: the distance to the edge of the
    /// nearest obstacle, in pixels (negative inside one). The log counts as
    /// a thick line: the distance to its centre line, less half its width.
    /// </summary>
    private float Clear(float px, float py)
    {
        float best = _u;
        for (int i = 0; i < _obsCentre.Length; i++)
        {
            float dx = px - _obsCentre[i].X, dy = py - _obsCentre[i].Y;
            best = MathF.Min(best, MathF.Sqrt(dx * dx + dy * dy) - _obsRadius[i]);
        }
        if (_log is Log log)
        {
            PointF a = log.At(0), b = log.At(1);
            float ex = b.X - a.X, ey = b.Y - a.Y, e2 = MathF.Max(1e-3f, ex * ex + ey * ey);
            float t = Math.Clamp(((px - a.X) * ex + (py - a.Y) * ey) / e2, 0, 1);
            float dx = px - (a.X + ex * t), dy = py - (a.Y + ey * t);
            best = MathF.Min(best, MathF.Sqrt(dx * dx + dy * dy) - log.Width * 0.5f);
        }
        return best;
    }

    /// <summary>
    /// The least room along a heading over the next "dist" pixels, felt at
    /// six points from just past the snout to the full distance. The near
    /// ones matter: a lily pad is smaller than the gap between far feelers,
    /// and the first build sat jammed against one for two minutes because
    /// every feeler landed in clear water beyond it.
    /// </summary>
    private float ClearAlong(float a, float dist)
    {
        float dx = MathF.Cos(a), dy = MathF.Sin(a), best = _u;
        foreach (float f in (ReadOnlySpan<float>)[0.08f, 0.2f, 0.35f, 0.55f, 0.78f, 1f])
            best = MathF.Min(best, Clear(_x + dx * dist * f, _y + dy * dist * f));
        return best;
    }

    /// <param name="animals">Points on the other animals this frame (koi heads and middles, turtles, the lizard), screen pixels.</param>
    public void Update(float dt, ReadOnlySpan<PointF> animals)
    {
        if (!_live)
        {
            _next -= dt;
            if (_next <= 0 && _often > 0) { Begin(); _next = (150 + 150 * (float)_rng.NextDouble()) / _often; }
            return;
        }
        _alive += dt;

        // ---- A touch from another animal: startle ----
        _calm -= dt;
        _flee = MathF.Max(0, _flee - dt);
        if (_calm <= 0)
        {
            float near = _u * 0.05f, near2 = near * near;
            for (int k = 0; k < animals.Length && _calm <= 0; k++)
                for (int i = 0; i < Points; i += 3)
                {
                    float dx = Spine[i].X - animals[k].X, dy = Spine[i].Y - animals[k].Y;
                    if (dx * dx + dy * dy > near2) continue;
                    // Away from whatever touched it, judged from its head.
                    _fleeAway = MathF.Atan2(_y - animals[k].Y, _x - animals[k].X);
                    _flee = FleeFor;
                    _calm = 9 + 6 * (float)_rng.NextDouble();
                    _ripples.Add(Spine[0].X, Spine[0].Y, 0.7f);
                    break;
                }
        }
        float fear = _flee / FleeFor;                      // 1 the instant it is touched, fading to 0 as it calms

        // ---- Speed: a steady swim, a burst when startled ----
        float wantSpeed = _u * 0.06f * (1 + 2.2f * fear);
        _speed += (wantSpeed - _speed) * (1 - MathF.Exp(-dt * (fear > 0 ? 6f : 1.2f)));

        // ---- Steering ----
        // A gentle pull toward its goal beyond the far edge, a slow wander,
        // and when startled a hard swing away from whatever touched it.
        float turn = Wrap(MathF.Atan2(_goal.Y - _y, _goal.X - _x) - _heading) * 0.5f;
        turn += 0.2f * MathF.Sin(_alive * 0.35f);
        if (fear > 0) turn += Wrap(_fleeAway - _heading) * 2.5f * fear;
        float most = 1.0f + 1.5f * fear;

        // Something in the way? Feel for the smallest turn that clears it.
        // "Enough room" is the head's own width a few times over; past that,
        // more room counts for nothing, so it skirts a pad rather than
        // heading for the far side of the pond.
        float look = _len * 0.8f + _speed * 0.8f, margin = _u * 0.035f;    // wider than the weave's swing, so the body clears too
        float clearAhead = _blocked ? 0 : ClearAlong(_heading, look);
        if (clearAhead < margin)
        {
            float best = _heading, bestScore = MathF.Min(clearAhead, margin * 2);
            for (int k = 1; k <= 5; k++)
                foreach (int side in (ReadOnlySpan<int>)[1, -1])
                {
                    float a = _heading + side * k * 0.35f;
                    float score = MathF.Min(ClearAlong(a, look), margin * 2) - k * margin * 0.15f;
                    if (score > bestScore) { bestScore = score; best = a; }
                }
            float urgency = Math.Clamp(1 - clearAhead / margin, 0, 1);
            turn += Wrap(best - _heading) * (1.5f + 3f * urgency);
            most = MathF.Max(most, 2.2f);
        }
        _heading = Wrap(_heading + Math.Clamp(turn, -most, most) * dt);

        // ---- Forward, unless the next step would be inside something ----
        float step = _speed * dt;
        float nx = _x + MathF.Cos(_heading) * step, ny = _y + MathF.Sin(_heading) * step;
        float clearNext = Clear(nx, ny);
        _blocked = !(clearNext > 0 || clearNext >= Clear(_x, _y));   // never stuck: a step toward more room is always allowed
        if (!_blocked)
        {
            _x = nx; _y = ny;
            _travelled += step;
        }
        // Gone: its head is far enough past an edge that its tail has left too.
        float m = _len * 1.25f;
        if (_alive > 3 && (_x < -m || _y < -m || _x > _pond.Width + m || _y > _pond.Height + m)) { _live = false; return; }

        // The weave: side to side, a full swing every 65% of a body length
        // travelled, so about one and a half waves lie along its body at once.
        // Distance drives it, so a burst of speed quickens the weave by itself.
        float weave = _len * 0.085f * MathF.Sin(_travelled / (_len * 0.65f) * MathF.Tau);
        float hx = MathF.Cos(_heading), hy = MathF.Sin(_heading);
        var head = new PointF(_x - hy * weave, _y + hx * weave * Ripples.Squash);
        if (Dist(head, _trail[_trailHead]) > _len / 150) Remember(head);

        // The wake: little rings pushed out from its head while it is in the water.
        _wakeTimer -= dt;
        int ix = (int)head.X, iy = (int)head.Y;
        if (_wakeTimer <= 0 && ix >= 0 && iy >= 0 && ix < _pond.Width && iy < _pond.Height && _pond.Water[iy * _pond.Width + ix])
        {
            _ripples.Add(head.X, head.Y, 0.4f + 0.2f * fear);
            _wakeTimer = 0.4f - 0.2f * fear;
        }
        LaySpine(head);
    }

    /// <summary>The spine, laid along the remembered trail, nose first.</summary>
    private void LaySpine(PointF head)
    {
        int slot = _trailHead, left = _trailCount - 1;
        PointF a = head, b = _trail[slot];
        float done = 0, seg = Dist(a, b);
        for (int i = 0; i < Points; i++)
        {
            float want = Along[i] * _len;
            while (done + seg < want && left > 0)
            {
                done += seg; a = b;
                slot = (slot - 1 + _trail.Length) % _trail.Length; left--;
                b = _trail[slot]; seg = Dist(a, b);
            }
            float t = seg > 1e-4f ? Math.Clamp((want - done) / seg, 0, 1) : 0;
            Spine[i] = new PointF(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
            HalfWidth[i] = _wide * Profile(Along[i]);
        }
    }

    /// <summary>
    /// How wide the snake is along its length: a rounded head a little wider
    /// than the neck behind it, the body full width, then tapering steadily
    /// to the tip of the tail.
    /// </summary>
    private static float Profile(float s)
    {
        if (s < 0.035f) return 0.55f + 0.65f * MathF.Sin(s / 0.035f * MathF.PI * 0.5f);   // the snout rounding out to the head
        if (s < 0.06f) return 1.2f - 0.4f * (s - 0.035f) / 0.025f;                        // the head narrowing to the neck
        if (s < 0.12f) return 0.8f + 0.2f * (s - 0.06f) / 0.06f;                          // the neck filling out
        if (s < 0.6f) return 1f;
        return MathF.Max(0.05f, 1 - (s - 0.6f) / 0.4f);                                    // the long taper to the tail
    }

    protected override bool Colour(float s, float v, out int r, out int g, out int b, out float alpha)
    {
        float av = MathF.Abs(v);
        // Olive back, darker on the head, with four dark stripes running down the body.
        (r, g, b) = s < 0.06f ? (112, 104, 62) : (150, 140, 88);
        bool stripe = s > 0.07f && (MathF.Abs(av - 0.32f) < 0.08f || MathF.Abs(av - 0.76f) < 0.08f);
        if (stripe) { r = 66; g = 58; b = 36; }
        float shade = 0.72f + 0.28f * MathF.Sqrt(MathF.Max(0, 1 - v * v));             // a rounded back
        r = (int)(r * shade); g = (int)(g * shade); b = (int)(b * shade);
        alpha = 1;
        return true;
    }

    public void Draw(FrameBuffer fb)
    {
        if (!_live) return;
        Paint(fb, _pond.Open, _pond.Sun, 0, MathF.Max(1f, _u * 0.0012f));
        // The eyes: tiny dark dots either side of the head.
        PointF e0 = Spine[1], e1 = Spine[2];
        float ex = e1.X - e0.X, ey = e1.Y - e0.Y, el = MathF.Max(1e-3f, MathF.Sqrt(ex * ex + ey * ey));
        float er = MathF.Max(0.7f, _wide * 0.3f);
        foreach (int side in (ReadOnlySpan<int>)[1, -1])
            Oval(fb, _pond.Open, new PointF(e0.X - ey / el * HalfWidth[1] * 0.7f * side, e0.Y + ex / el * HalfWidth[1] * 0.7f * side), 0, er, er, 20, 20, 16, 0.9f, 0, _pond.Sun);
    }
}
