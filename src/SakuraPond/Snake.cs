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
/// So we move only the head (forward along its route, weaving), remember
/// where it has been, and lay the body along that trail (Body.cs draws it
/// bending). Its head pushes a small V-shaped wake out across the water.
/// </summary>
internal sealed class Snake : Body
{
    private const int Points = 44;

    private readonly Pond _pond;
    private readonly Ripples _ripples;
    private readonly Random _rng;
    private readonly float _u, _often, _len, _wide;
    private readonly PointF[] _trail = new PointF[400];
    private int _trailHead, _trailCount;
    private PointF[] _route = [];
    private float[] _routeAt = [];               // distance along the route at each of its points
    private float _travelled, _routeLength, _wakeTimer, _next;
    private bool _live;

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
    }

    /// <summary>
    /// Plans a crossing: from beyond one edge of the screen, through the
    /// pond, out beyond the far side, on a gentle curve.
    /// </summary>
    private void Begin()
    {
        float a = (float)(_rng.NextDouble() * MathF.Tau), b = a + MathF.PI + ((float)_rng.NextDouble() - 0.5f) * 1.2f;
        PointF from = OffScreen(a), to = OffScreen(b);
        // A gentle bend: the middle of the route pushed a little to one side.
        PointF mid = new((from.X + to.X) / 2, (from.Y + to.Y) / 2);
        float dx = to.X - from.X, dy = to.Y - from.Y, d = MathF.Sqrt(dx * dx + dy * dy);
        float bend = ((float)_rng.NextDouble() - 0.5f) * 0.4f * d;
        PointF ctrl = new(mid.X - dy / d * bend, mid.Y + dx / d * bend);
        _route = new PointF[80];
        _routeAt = new float[80];
        for (int i = 0; i < _route.Length; i++)
        {
            float t = i / (_route.Length - 1f), m = 1 - t;
            _route[i] = new PointF(m * m * from.X + 2 * m * t * ctrl.X + t * t * to.X, m * m * from.Y + 2 * m * t * ctrl.Y + t * t * to.Y);
            if (i > 0) _routeAt[i] = _routeAt[i - 1] + Dist(_route[i], _route[i - 1]);
        }
        _routeLength = _routeAt[^1];
        _travelled = 0;
        _trailCount = 0;
        // Its body starts laid out behind it, off the screen.
        PointF back = new(from.X - (_route[1].X - from.X) / Dist(_route[1], from) * _len * 1.2f, from.Y - (_route[1].Y - from.Y) / Dist(_route[1], from) * _len * 1.2f);
        for (int i = 0; i <= 60; i++) Remember(new PointF(back.X + (from.X - back.X) * i / 60, back.Y + (from.Y - back.Y) * i / 60));
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

    private void Remember(PointF p)
    {
        _trailHead = (_trailHead + 1) % _trail.Length;
        _trail[_trailHead] = p;
        _trailCount = Math.Min(_trail.Length, _trailCount + 1);
    }

    public void Update(float dt)
    {
        if (!_live)
        {
            _next -= dt;
            if (_next <= 0 && _often > 0) { Begin(); _next = (150 + 150 * (float)_rng.NextDouble()) / _often; }
            return;
        }
        _travelled += _u * 0.06f * dt;                    // an unhurried swim: a screen height in about fifteen seconds
        if (_travelled > _routeLength + _len * 1.2f) { _live = false; return; }

        // Where it is along the route, and which way the route runs there.
        float along = MathF.Min(_travelled, _routeLength);
        int k = 1;
        while (k < _route.Length - 1 && _routeAt[k] < along) k++;
        float f = (along - _routeAt[k - 1]) / MathF.Max(1e-3f, _routeAt[k] - _routeAt[k - 1]);
        PointF a = _route[k - 1], b = _route[k];
        PointF basePt = new(a.X + (b.X - a.X) * f, a.Y + (b.Y - a.Y) * f);
        float ex = b.X - a.X, ey = b.Y - a.Y, el = MathF.Max(1e-3f, MathF.Sqrt(ex * ex + ey * ey));
        if (_travelled > _routeLength)                     // past the end: carry straight on out of sight
            basePt = new PointF(basePt.X + ex / el * (_travelled - _routeLength), basePt.Y + ey / el * (_travelled - _routeLength));
        // The weave: side to side, a full swing every 65% of a body length
        // travelled, so about one and a half waves lie along its body at once.
        float weave = _len * 0.085f * MathF.Sin(_travelled / (_len * 0.65f) * MathF.Tau);
        var head = new PointF(basePt.X - ey / el * weave, basePt.Y + ex / el * weave * Ripples.Squash);
        if (Dist(head, _trail[_trailHead]) > _len / 150) Remember(head);

        // The wake: little rings pushed out from its head while it is in the water.
        _wakeTimer -= dt;
        int ix = (int)head.X, iy = (int)head.Y;
        if (_wakeTimer <= 0 && ix >= 0 && iy >= 0 && ix < _pond.Width && iy < _pond.Height && _pond.Water[iy * _pond.Width + ix])
        {
            _ripples.Add(head.X, head.Y, 0.4f);
            _wakeTimer = 0.4f;
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
