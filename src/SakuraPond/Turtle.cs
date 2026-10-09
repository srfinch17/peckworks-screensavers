using Peckworks.Screensavers.Core;

namespace SakuraPond;

/// <summary>The pond's turtles, drawn after the koi (a turtle keeps near the surface, so it is above them).</summary>
internal sealed class Turtles
{
    private readonly Turtle[] _all;

    public Turtles(Pond pond, int count, Ripples ripples, bool[] where, Random rng)
    {
        _all = new Turtle[Math.Max(0, count)];
        for (int i = 0; i < _all.Length; i++) _all[i] = new Turtle(pond, ripples, where, rng);
    }

    public void Update(float dt)
    {
        foreach (Turtle t in _all) t.Update(dt);
    }

    public void Draw(FrameBuffer fb)
    {
        foreach (Turtle t in _all) t.Draw(fb);
    }
}

/// <summary>
/// A pond turtle seen from above: a red-eared slider, the turtle of nearly
/// every park pond in Japan (olive shell, striped head, a red streak behind
/// each eye).
///
/// WHAT A POND TURTLE DOES (watch one at any pond): it paddles slowly just
/// under the surface, FRONT LEFT leg together with BACK RIGHT, then the other
/// pair, like a dog swimming, with a small surge on each stroke. Its shell
/// is rigid, so it turns as one piece, in wide slow arcs, by paddling harder
/// on one side; nothing bends. Every so often it stops, spreads its legs and
/// just floats at the surface for a while, pokes its nose up for a breath
/// (one small ring), then paddles off again.
///
/// HOW IT IS DRAWN: along a SPINE like the koi (Body.cs), only the spine is
/// a straight line from nose to tail that turns as a whole. The colours at
/// each spot (how far along, how far across) are worked out once: the head's
/// stripes, the shell's plates (a middle row of five, a row of four each
/// side, and a ring of small ones round the rim), the dome's shading. The
/// four legs are small ovals that swing from under the shell's edge.
/// </summary>
internal sealed class Turtle : Body
{
    private const int Points = 14;
    private const int SkinLen = 256, SkinWide = 64;

    private readonly Pond _pond;
    private readonly Ripples _ripples;
    private readonly bool[] _where;
    private readonly Random _rng;
    private readonly float _len, _wide;                   // nose to tail tip, and half the shell's width, ground pixels
    private readonly uint[] _skin;

    private float _x, _y, _heading, _speed, _phase, _stroke = 1;   // _stroke: 1 paddling, 0 legs still
    private float _goalX, _goalY, _goalTimer, _restTimer, _zTarget, _depthTimer;
    private bool _resting;
    private double _time;

    /// <summary>How deep it is: 0 at the surface, 1 as deep as a koi goes. A turtle stays near the top.</summary>
    public float Z { get; private set; }

    public Turtle(Pond pond, Ripples ripples, bool[] where, Random rng) : base(Points)
    {
        _pond = pond;
        _ripples = ripples;
        _where = where;
        _rng = rng;
        _len = pond.U * 0.1f;
        _wide = _len * 0.24f;
        _skin = Skin(rng);
        for (int i = 0; i < Points; i++) Along[i] = i / (Points - 1f);

        for (int tries = 0; tries < 500; tries++)
        {
            float x = (float)rng.NextDouble() * pond.Width, y = (float)rng.NextDouble() * pond.Height;
            if (pond.RoomAt(x, y) < _len * 1.4f) continue;
            (_x, _y) = ToGround(x, y);
            break;
        }
        _heading = (float)(rng.NextDouble() * MathF.Tau);
        _speed = _len * 0.4f;
        Z = 0.1f;
        _zTarget = Z;
        _depthTimer = 5 + 10 * (float)rng.NextDouble();
        _restTimer = 20 + 40 * (float)rng.NextDouble();
        NewGoal();
        LaySpine();
    }

    private float CentreY => _pond.Centre.Y;
    private (float, float) ToGround(float x, float y) => (x, CentreY + (y - CentreY) / Ripples.Squash);
    private PointF ToScreen(float gx, float gy) => new(gx, CentreY + (gy - CentreY) * Ripples.Squash);
    private static float Wrap(float a) => MathF.IEEERemainder(a, MathF.Tau);
    private static float Dist(PointF a, PointF b) => MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    public void Update(float dt)
    {
        _time += dt;
        float t = (float)(_time % 100000.0);

        // ---- Paddle, or float and rest ----
        _restTimer -= dt;
        if (_restTimer <= 0)
        {
            _resting = !_resting;
            if (_resting)
            {
                _restTimer = 8 + 14 * (float)_rng.NextDouble();
                _zTarget = 0;                                           // up to the surface for a breath
                PointF nose = Spine[0];
                _ripples.Add(nose.X, nose.Y, 0.5f);
            }
            else
            {
                _restTimer = 25 + 40 * (float)_rng.NextDouble();
                NewGoal();
            }
        }
        _stroke += ((_resting ? 0f : 1f) - _stroke) * (1 - MathF.Exp(-dt * 1.5f));
        _phase = (_phase + MathF.Tau * 1.1f * dt * _stroke) % MathF.Tau;
        // Each pair of legs pushes as it sweeps back: a small surge twice a cycle.
        float targetSpeed = _resting ? 0 : _len * 0.5f * (0.75f + 0.5f * MathF.Abs(MathF.Cos(_phase)));
        _speed += (targetSpeed - _speed) * (1 - MathF.Exp(-dt * (_resting ? 0.8f : 2.5f)));

        // ---- Steering: a slow wander, a pull toward its goal, and the bank kept off ----
        PointF here = ToScreen(_x, _y);
        if (!_resting)
        {
            float turn = 0.15f * MathF.Sin(t * 0.17f);
            _goalTimer -= dt;
            float gdx = _goalX - _x, gdy = _goalY - _y;
            if (_goalTimer <= 0 || gdx * gdx + gdy * gdy < _len * _len * 4) NewGoal();
            turn += Wrap(MathF.Atan2(gdy, gdx) - _heading) * 0.3f;
            float roomAhead = RoomAlong(_heading, _len * 1.6f), roomHere = _pond.RoomAt(here.X, here.Y);
            float most = 0.35f;
            if (roomAhead < _len * 0.9f || roomHere < _len * 0.5f)
            {
                // The bank is coming: swing toward more open water, and slow down.
                PointF open = _pond.OpenWay(here.X, here.Y);
                if (open.X * open.X + open.Y * open.Y > 1e-6f) turn += Wrap(MathF.Atan2(open.Y, open.X) - _heading) * 2f;
                most = 1.0f;
                _speed *= 1 - 0.8f * dt;
            }
            _heading = Wrap(_heading + Math.Clamp(turn, -most, most) * dt);
        }
        float step = _speed * dt;
        PointF next = ToScreen(_x + MathF.Cos(_heading) * step, _y + MathF.Sin(_heading) * step);
        float roomNext = _pond.RoomAt(next.X, next.Y);
        if (roomNext > _len * 0.2f || roomNext >= _pond.RoomAt(here.X, here.Y))   // never stuck: toward more room is always allowed
        {
            _x += MathF.Cos(_heading) * step;
            _y += MathF.Sin(_heading) * step;
        }

        // ---- Depth: near the surface, a little deeper now and then ----
        _depthTimer -= dt;
        if (_depthTimer <= 0 && !_resting)
        {
            _zTarget = 0.03f + 0.3f * (float)_rng.NextDouble();
            _depthTimer = 8 + 10 * (float)_rng.NextDouble();
        }
        int ix = Math.Clamp((int)here.X, 0, _pond.Width - 1), iy = Math.Clamp((int)here.Y, 0, _pond.Height - 1);
        float bottom = 0.05f + 0.9f * _pond.Depth[iy * _pond.Width + ix] / 255f;
        Z += (MathF.Min(_zTarget, bottom) - Z) * (1 - MathF.Exp(-dt * 0.3f));

        LaySpine();
    }

    private void NewGoal()
    {
        for (int tries = 0; tries < 60; tries++)
        {
            float x = (float)_rng.NextDouble() * _pond.Width, y = (float)_rng.NextDouble() * _pond.Height;
            if (_pond.RoomAt(x, y) < _len * 0.6f) continue;
            (_goalX, _goalY) = ToGround(x, y);
            break;
        }
        _goalTimer = 20 + 25 * (float)_rng.NextDouble();
    }

    /// <summary>The open water "dist" ahead along heading a, checked halfway too.</summary>
    private float RoomAlong(float a, float dist)
    {
        PointF mid = ToScreen(_x + MathF.Cos(a) * dist * 0.5f, _y + MathF.Sin(a) * dist * 0.5f);
        PointF end = ToScreen(_x + MathF.Cos(a) * dist, _y + MathF.Sin(a) * dist);
        return MathF.Min(_pond.RoomAt(end.X, end.Y), _pond.RoomAt(mid.X, mid.Y) * 1.5f);
    }

    /// <summary>A point of the body in screen pixels: s along from the nose (0 to 1), v across (-1 left edge to +1 right edge).</summary>
    private PointF At(float s, float v)
    {
        float dx = MathF.Cos(_heading), dy = MathF.Sin(_heading);
        float gx = _x + dx * s * _len - dy * v * _wide * Profile(s), gy = _y + dy * s * _len + dx * v * _wide * Profile(s);
        return ToScreen(gx, gy);
    }

    /// <summary>The straight spine from nose to tail, each pair of edge points squashed to the screen.</summary>
    private void LaySpine()
    {
        for (int i = 0; i < Points; i++)
        {
            PointF l = At(Along[i], -1), r = At(Along[i], 1);
            Spine[i] = new PointF((l.X + r.X) / 2, (l.Y + r.Y) / 2);
            HalfWidth[i] = Dist(l, r) / 2;
        }
    }

    /// <summary>
    /// How wide the body is along its length, as a fraction of the shell's
    /// half width: a rounded head on a neck, the oval shell from 0.22 to
    /// 0.88, and a short tail tapering to a point.
    /// </summary>
    private static float Profile(float s)
    {
        float head = 0;
        if (s < 0.04f) { float k = (0.04f - s) / 0.04f; head = 0.22f * MathF.Sqrt(MathF.Max(0, 1 - k * k)); }
        else if (s < 0.14f) head = 0.22f;
        else if (s < 0.22f) head = 0.22f - 0.06f * (s - 0.14f) / 0.08f;
        float shell = 0;
        if (s >= 0.22f && s <= 0.88f) { float k = (s - 0.55f) / 0.33f; shell = MathF.Sqrt(MathF.Max(0, 1 - k * k)); }
        float tail = s > 0.88f ? MathF.Max(0.03f, 0.12f - 0.09f * (s - 0.88f) / 0.12f) : 0;
        float neck = s >= 0.14f && s <= 0.9f ? 0.16f : 0;
        return MathF.Max(MathF.Max(head, shell), MathF.Max(tail, neck));
    }

    protected override bool Colour(float s, float v, out int r, out int g, out int b, out float alpha)
    {
        int vi = Math.Clamp((int)((v + 1) * 0.5f * SkinWide), 0, SkinWide - 1);
        uint c = _skin[Math.Clamp((int)(s * SkinLen), 0, SkinLen - 1) * SkinWide + vi];
        r = (int)((c >> 16) & 0xFF); g = (int)((c >> 8) & 0xFF); b = (int)(c & 0xFF);
        alpha = 1;
        return true;
    }

    /// <summary>
    /// The colours at every (s, v), worked out once. Across the shell, v is
    /// measured as a fraction of the shell's width THERE, so a band at
    /// |v| near 1 runs all the way round the rim: that is the ring of small
    /// marginal plates. The middle row (|v| small) and the side rows between
    /// are split along the shell by cross seams. The seams are pale, each
    /// plate a little lighter toward its own middle, and the whole dome is
    /// shaded brightest along the backbone.
    /// </summary>
    private static uint[] Skin(Random rng)
    {
        var skin = new uint[SkinLen * SkinWide];
        float hue = (float)rng.NextDouble();                            // a little variety: browner or greener shells
        (float R, float G, float B) shell = (66 + 20 * hue, 80 - 6 * hue, 40), seam = (118, 128, 66), skin0 = (74, 92, 48), stripe = (214, 196, 80);
        for (int si = 0; si < SkinLen; si++)
            for (int vi = 0; vi < SkinWide; vi++)
            {
                float s = (si + 0.5f) / SkinLen, v = (vi + 0.5f) / SkinWide * 2 - 1, av = MathF.Abs(v);
                (float R, float G, float B) c;
                if (s < 0.22f)
                {
                    // Head and neck: olive, two yellow stripes running back from the eyes, the red "ear" behind them.
                    c = skin0;
                    if (s > 0.03f && MathF.Abs(av - 0.5f) < 0.1f) c = Mix(c, stripe, 0.75f);
                    if (s > 0.1f && s < 0.19f && av > 0.62f && av < 0.95f) c = Mix(c, (196, 58, 40), 0.85f);
                    float dome = 0.7f + 0.3f * MathF.Sqrt(MathF.Max(0, 1 - v * v));
                    c = (c.R * dome, c.G * dome, c.B * dome);
                }
                else if (s <= 0.88f)
                {
                    float along = (s - 0.22f) / 0.66f;                  // 0 at the shell's front, 1 at its back
                    c = shell;
                    // Which plate: the rim ring, the side rows or the middle row.
                    int cols = av > 0.82f ? 10 : 5;                     // marginals are smaller: twice as many along
                    float ps = along * cols, pv = av > 0.82f ? 0 : (av - 0.36f) / 0.46f;   // position inside the plate, along
                    float plateS = ps - MathF.Floor(ps) - 0.5f;
                    float plateV = av > 0.82f ? (av - 0.91f) / 0.09f : av < 0.36f ? v / 0.36f : (av - 0.59f) / 0.23f;
                    float inside = 1 - MathF.Max(MathF.Abs(plateS) * 2, MathF.Abs(plateV));
                    c = Mix(c, (104, 116, 60), 0.35f * MathF.Max(0, inside));
                    // The seams.
                    bool seamS = MathF.Abs(plateS) > 0.5f - 0.022f;
                    bool seamV = MathF.Abs(av - 0.36f) < 0.012f || MathF.Abs(av - 0.82f) < 0.012f;
                    if (seamS || seamV) c = Mix(c, seam, 0.7f);
                    // The dome: brightest along the backbone and over the middle of its length, darker to the rim.
                    float dome = (0.62f + 0.38f * MathF.Sqrt(MathF.Max(0, 1 - v * v))) * (0.86f + 0.14f * MathF.Sin(along * MathF.PI));
                    c = (c.R * dome, c.G * dome, c.B * dome);
                }
                else
                {
                    c = (60, 74, 40);
                }
                skin[si * SkinWide + vi] = (uint)(Math.Min(255, (int)c.R) << 16 | Math.Min(255, (int)c.G) << 8 | Math.Min(255, (int)c.B));
            }
        return skin;
    }

    private static (float, float, float) Mix((float R, float G, float B) a, (float R, float G, float B) b, float k) =>
        (a.R + (b.R - a.R) * k, a.G + (b.G - a.G) * k, a.B + (b.B - a.B) * k);

    public void Draw(FrameBuffer fb)
    {
        float murk = 0.06f + 0.62f * Z;
        float soft = MathF.Max(1f, _pond.U * 0.0012f) * (1 + 3 * Z);
        // Legs first, so the shell covers where they join it.
        foreach (int side in (ReadOnlySpan<int>)[1, -1])
        {
            // Front left with back right, then the other pair: the diagonal pairs share a phase.
            float front = _phase + (side > 0 ? 0 : MathF.PI), rear = _phase + (side > 0 ? MathF.PI : 0);
            Leg(fb, 0.3f, side, 1.22f + 0.7f * MathF.Sin(front) * _stroke - 0.17f * (1 - _stroke), 0.34f, murk);
            Leg(fb, 0.74f, side, 2.36f + 0.5f * MathF.Sin(rear) * _stroke + 0.26f * (1 - _stroke), 0.3f, murk);
        }
        Paint(fb, _where, _pond.Sun, murk, soft);
        // The eyes: a dark dot each side of the head, toward the front.
        float er = MathF.Max(0.8f, _len * 0.014f);
        foreach (int side in (ReadOnlySpan<int>)[1, -1])
            Oval(fb, _where, At(0.07f, side * 0.7f), 0, er, er, 20, 24, 16, 0.95f, murk * 0.7f, _pond.Sun);
    }

    /// <summary>
    /// One leg: a broad flat paddle from under the shell's edge at s, on one
    /// side, swung out at "angle" from straight ahead (0 = forward, pi =
    /// backward), with the webbed foot at its end.
    /// </summary>
    private void Leg(FrameBuffer fb, float s, int side, float angle, float length, float murk)
    {
        float a = _heading + side * angle;
        float dx = MathF.Cos(a), dy = MathF.Sin(a);
        float hdx = MathF.Cos(_heading), hdy = MathF.Sin(_heading);
        float l = _len * length;
        // The hip, just inside the shell's edge, in ground pixels.
        float hw = _wide * Profile(s) * 0.8f;
        float hx = _x + hdx * s * _len - hdy * side * hw, hy = _y + hdy * s * _len + hdx * side * hw;
        PointF thigh = ToScreen(hx + dx * l * 0.4f, hy + dy * l * 0.4f), foot = ToScreen(hx + dx * l * 0.85f, hy + dy * l * 0.85f);
        Oval(fb, _where, thigh, a, l * 0.5f, l * 0.2f, 66, 84, 44, 1f, murk, _pond.Sun, rays: false);
        Oval(fb, _where, foot, a, l * 0.3f, l * 0.17f, 56, 74, 40, 1f, murk, _pond.Sun, rays: false);
    }
}
