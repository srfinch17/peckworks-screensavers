using System.Drawing.Drawing2D;
using Peckworks.Screensavers.Core;

namespace SakuraPond;

/// <summary>
/// Barn swallows (tsubame) visiting the pond to drink. They arrive in Japan
/// in spring, with the cherry blossom.
///
/// WHAT SWALLOWS DO OVER WATER: they come in low and fast, sweeping round
/// the pond in long, smooth curves, a burst of quick wingbeats and then a
/// glide with the wings swept back. Every so often one drops right down and
/// skims the surface to drink on the wing, its bill just touching, leaving a
/// short line of rings. A pair makes a few passes, then is gone.
///
/// Seen from above: a dark, glossy blue back, long pointed wings and the long
/// forked tail. A wingbeat shows from above as the wings' span shrinking and
/// growing (a wing raised or lowered looks shorter from overhead), so the
/// flap is eight pictures: six steps of the beat, and two glides with the
/// wings swept further back. The body is rigid, so each picture is turned to
/// face its way of flight (TurningStamp), never flipped. Their shadows race
/// across the water below them.
/// </summary>
internal sealed class Swallows
{
    private struct Bird
    {
        public float X, Y, Height, Heading, Speed;
        public float FlapTimer, FlapPhase;
        public bool Flapping;
        public int Leg;                     // which waypoint it is heading for
        public bool Skimming, Leaving;
        public float RingTimer;
        public bool Live;
        public float LegTime;               // how long it has been on this leg
    }

    private readonly Pond _pond;
    private readonly Ripples _ripples;
    private readonly Random _rng;
    private readonly float _u, _often;
    private readonly TurningStamp[] _looks;
    private readonly Bird[] _birds = new Bird[2];
    private readonly PointF[][] _routes = new PointF[2][];
    private float _nextVisit;

    public Swallows(Pond pond, Ripples ripples, float often, Random rng)
    {
        _pond = pond;
        _ripples = ripples;
        _rng = rng;
        _u = pond.U;
        _often = often;
        _looks = Looks();
        _nextVisit = 8 + 20 * (float)rng.NextDouble();      // the first visit comes soon
    }

    /// <summary>Eight pictures of a swallow facing right: six steps of a wingbeat, two glides.</summary>
    private TurningStamp[] Looks()
    {
        float span = _u * 0.075f, len = _u * 0.05f;
        int size = (int)MathF.Ceiling(MathF.Max(span, len * 1.6f) * 1.1f) + 4;
        float c = size / 2f;
        var back = Color.FromArgb(28, 38, 70);
        var looks = new TurningStamp[8];
        for (int k = 0; k < 8; k++)
        {
            float reach, sweep;
            if (k < 6) { reach = 0.62f + 0.38f * MathF.Cos(k * MathF.Tau / 6); sweep = 18; }   // the beat: full span, shorter, shortest, back to full
            else { reach = 0.98f; sweep = k == 6 ? 32 : 44; }                              // gliding, wings swept back
            looks[k] = new TurningStamp(size, size, g =>
            {
                // Tail: two long streamers, forked.
                using (var tail = new Pen(back, MathF.Max(0.8f, len * 0.035f)) { StartCap = LineCap.Round, EndCap = LineCap.Triangle })
                {
                    g.DrawLine(tail, c - len * 0.2f, c, c - len * 0.78f, c - len * 0.1f);
                    g.DrawLine(tail, c - len * 0.2f, c, c - len * 0.78f, c + len * 0.1f);
                }
                using (var tailBase = new SolidBrush(back))
                    g.FillPolygon(tailBase, [new PointF(c - len * 0.1f, c - len * 0.06f), new PointF(c - len * 0.42f, c - len * 0.07f), new PointF(c - len * 0.42f, c + len * 0.07f), new PointF(c - len * 0.1f, c + len * 0.06f)]);
                // Wings: long, narrow, pointed scythes from the shoulders.
                foreach (float side in (ReadOnlySpan<float>)[-1, 1])
                {
                    float wl = span * 0.5f * reach;
                    float a = (90 + sweep) * MathF.PI / 180;          // out to the side and swept back
                    PointF root = new(c + len * 0.12f, c + side * len * 0.04f);
                    PointF tip = new(root.X + MathF.Cos(a) * wl, root.Y + side * MathF.Sin(a) * wl);
                    PointF lead = new(root.X + MathF.Cos(a - 0.35f) * wl * 0.55f, root.Y + side * MathF.Sin(a - 0.35f) * wl * 0.55f);
                    PointF trail = new(c - len * 0.05f, c + side * len * 0.05f);
                    using var wing = new GraphicsPath();
                    wing.AddBezier(root, lead, new PointF(tip.X + len * 0.05f, tip.Y - side * len * 0.02f), tip);
                    wing.AddBezier(tip, new PointF((tip.X + trail.X) / 2 - len * 0.05f, (tip.Y + trail.Y) / 2), trail, trail);
                    wing.CloseFigure();
                    using var wb = new LinearGradientBrush(root, tip, Color.FromArgb(36, 48, 86), Color.FromArgb(22, 26, 40));
                    g.FillPath(wb, wing);
                }
                // Body: a slim spindle, glossy blue-black, a touch lighter down the back where the light catches it.
                using (var body = new GraphicsPath())
                {
                    body.AddEllipse(c - len * 0.24f, c - len * 0.075f, len * 0.5f, len * 0.15f);
                    using var bb = new LinearGradientBrush(new PointF(0, c - len * 0.08f), new PointF(0, c + len * 0.08f), Color.FromArgb(52, 70, 120), Color.FromArgb(20, 26, 46));
                    g.FillPath(bb, body);
                }
                // The rusty forehead and the head, at the front.
                using (var head = new SolidBrush(Color.FromArgb(30, 36, 60)))
                    g.FillEllipse(head, c + len * 0.16f, c - len * 0.06f, len * 0.13f, len * 0.12f);
                using (var face = new SolidBrush(Color.FromArgb(150, 72, 44)))
                    g.FillEllipse(face, c + len * 0.25f, c - len * 0.025f, len * 0.05f, len * 0.05f);
            });
        }
        return looks;
    }

    /// <summary>A path for one visit: in from beyond one edge, a few sweeps round the pond, out beyond another.</summary>
    private PointF[] Route()
    {
        var pts = new List<PointF>();
        PointF Edge()
        {
            float a = (float)(_rng.NextDouble() * MathF.Tau);
            return new PointF(_pond.Centre.X + MathF.Cos(a) * _pond.Width * 0.8f, _pond.Centre.Y + MathF.Sin(a) * _pond.Height * 0.8f);
        }
        pts.Add(Edge());
        int sweeps = 3 + _rng.Next(4);
        for (int i = 0; i < sweeps; i++)
        {
            float a = (float)(_rng.NextDouble() * MathF.Tau), d = 0.3f + 0.65f * (float)_rng.NextDouble();
            pts.Add(new PointF(_pond.Centre.X + MathF.Cos(a) * _pond.Reach.Width * d, _pond.Centre.Y + MathF.Sin(a) * _pond.Reach.Height * d));
        }
        pts.Add(Edge());
        return pts.ToArray();
    }

    public void Update(float dt)
    {
        _nextVisit -= dt;
        if (_nextVisit <= 0 && _often > 0 && !_birds[0].Live && !_birds[1].Live)
        {
            _nextVisit = (50 + 60 * (float)_rng.NextDouble()) / _often;
            int pair = _rng.NextDouble() < 0.6 ? 2 : 1;
            for (int i = 0; i < pair; i++)
            {
                _routes[i] = Route();
                PointF s = _routes[i][0];
                _birds[i] = new Bird
                {
                    X = s.X + i * _u * 0.12f, Y = s.Y + i * _u * 0.05f, Height = 0.14f + 0.04f * i,
                    Speed = _u * (0.6f + 0.15f * (float)_rng.NextDouble()), Leg = 1, Live = true,
                    Heading = MathF.Atan2(_routes[i][1].Y - s.Y, _routes[i][1].X - s.X),
                    FlapTimer = 0.5f, Flapping = true, FlapPhase = (float)_rng.NextDouble() * 6,
                };
            }
        }
        for (int i = 0; i < _birds.Length; i++)
        {
            ref Bird b = ref _birds[i];
            if (!b.Live) continue;
            PointF[] route = _routes[i];
            PointF to = route[b.Leg];
            float dx = to.X - b.X, dy = (to.Y - b.Y) / Ripples.Squash;
            float d = MathF.Sqrt(dx * dx + dy * dy);
            b.LegTime += dt;
            // Passed the point? Close to it and now flying away from it, or
            // simply long enough on this leg. (A swallow at speed turns in a
            // wider circle than "close enough", so waiting to hit the point
            // exactly would have it circling the point for ever.)
            bool goingAway = dx * MathF.Cos(b.Heading) + dy * MathF.Sin(b.Heading) < 0;
            if (d < _u * 0.12f || (goingAway && d < _u * 0.45f) || b.LegTime > 4.5f)
            {
                b.LegTime = 0;
                b.Leg++;
                if (b.Leg >= route.Length) { b.Live = false; continue; }
                // On some sweeps it drops down to skim the water and drink.
                b.Skimming = b.Leg < route.Length - 1 && _rng.NextDouble() < 0.45;
                to = route[b.Leg];
                dx = to.X - b.X; dy = (to.Y - b.Y) / Ripples.Squash;
            }
            // Steer toward the next point in a smooth curve: a swallow cannot turn on the spot at speed.
            float want = MathF.Atan2(dy, dx);
            float turn = Math.Clamp(MathF.IEEERemainder(want - b.Heading, MathF.Tau) * 2.2f, -2.4f, 2.4f);
            b.Heading = MathF.IEEERemainder(b.Heading + turn * dt, MathF.Tau);
            b.X += MathF.Cos(b.Heading) * b.Speed * dt;
            b.Y += MathF.Sin(b.Heading) * b.Speed * dt * Ripples.Squash;
            // Height: down to the water to skim, else a little above it.
            int ix = Math.Clamp((int)b.X, 0, _pond.Width - 1), iy = Math.Clamp((int)b.Y, 0, _pond.Height - 1);
            bool overWater = _pond.Water[iy * _pond.Width + ix];
            float wantH = b.Skimming && overWater ? 0.004f : 0.1f + 0.04f * i;
            b.Height += (wantH - b.Height) * (1 - MathF.Exp(-dt * 2.2f));
            if (b.Skimming && overWater && b.Height < 0.012f)
            {
                b.RingTimer -= dt;
                if (b.RingTimer <= 0)
                {
                    // The bill touches: a little ring just in front of the body.
                    _ripples.Add(b.X + MathF.Cos(b.Heading) * _u * 0.02f, b.Y + MathF.Sin(b.Heading) * _u * 0.02f * Ripples.Squash, 0.9f);
                    b.RingTimer = 0.11f;
                }
            }
            // Wings: a burst of beats, then a glide.
            b.FlapTimer -= dt;
            if (b.FlapTimer <= 0)
            {
                b.Flapping = !b.Flapping;
                b.FlapTimer = b.Flapping ? 0.35f + 0.5f * (float)_rng.NextDouble() : 0.5f + 0.9f * (float)_rng.NextDouble();
            }
            if (b.Flapping) b.FlapPhase = (b.FlapPhase + dt * 6 * 7f) % 6;    // seven beats a second, six pictures a beat
        }
    }

    private int LookOf(in Bird b) => b.Flapping ? (int)b.FlapPhase % 6 : (b.Height < 0.03f ? 7 : 6);

    public void DrawShadows(FrameBuffer fb)
    {
        var shade = Color.FromArgb(8, 22, 24);
        foreach (Bird b in _birds)
        {
            if (!b.Live) continue;
            float h = b.Height * _u;
            _looks[LookOf(b)].Draw(fb, b.X + h * Light.ShadowX, b.Y + h * Light.ShadowY, b.Heading, 1f, Ripples.Squash, 0.3f, shade, _pond.Open);
        }
    }

    public void Draw(FrameBuffer fb)
    {
        foreach (Bird b in _birds)
        {
            if (!b.Live) continue;
            _looks[LookOf(b)].Draw(fb, b.X, b.Y - b.Height * _u * Light.Lift, b.Heading, 1 + b.Height * 0.9f, Ripples.Squash, 1f, null, _pond.Open);
        }
    }
}
