using Peckworks.Screensavers.Core;

namespace Christmas.Happenings;

/// <summary>
/// A toy train, far away, crossing the valley. It runs along the flat valley
/// floor just below the near mountains' feet, from off one side of the screen
/// to off the other, puffing pale smoke. Think of a model railway seen from
/// across a big room: tiny, dark, with a few lit windows.
///
/// It is a dark blue-black silhouette, because against the pale blue valley
/// a dark shape reads at once; the little warm yellow windows and the
/// headlight are what make it feel alive and cozy.
///
/// It is drawn "only where the valley is open" (the OpenValley stencil), so
/// the snowy hill, the pines and the cabin hide it as it passes behind them.
///
/// How it moves: the train's left edge is one straight sweep across the
/// screen, worked out from t. The wheels are a flip-book of four poses (one
/// little pale dot per wheel, a quarter turn further round in each). The
/// smoke is a string of puffs: puff number j leaves the funnel at time
/// j * 0.4 s, from wherever the funnel was THEN, and from t alone we know
/// how old it is, so how high it has floated and how faded it is.
/// </summary>
internal sealed class ToyTrain : Happening
{
    private const int Poses = 4;                           // wheel flip-book pages
    // Seconds to cross. Worked out from a fixed SPEED (0.14 U per second),
    // so the train does not race on a very wide screen.
    private readonly float _trip;
    private const float PuffEvery = 0.4f;                  // a new puff this often
    private const float PuffLife = 2.4f;

    private static readonly Color Body = Color.FromArgb(24, 30, 62);
    private static readonly Color Lit = Color.FromArgb(255, 214, 110);
    private static readonly Color Smoke = Color.FromArgb(235, 240, 250);

    private readonly ChristmasScenery _s;
    private readonly Sprite[,] _train = new Sprite[2, Poses];   // [0 = heading right, 1 = heading left, wheel pose]
    private readonly Sprite _lamp, _puffSmall, _puffBig;
    private readonly int _len, _h;
    private readonly float _funnelX;                       // where the funnel is, in a right-heading sprite, from its left edge
    private bool _left;                                    // this showing's direction

    public override float Seconds => _trip;

    public ToyTrain(ChristmasScenery s)
    {
        _s = s;
        _len = Math.Max(24, (int)(s.U * 0.12f));
        _trip = (s.Width + _len) / (0.14f * s.U);            // distance divided by speed
        _h = Math.Max(6, (int)(s.U * 0.012f));
        _funnelX = _len * 0.925f;
        for (int pose = 0; pose < Poses; pose++)
        {
            _train[0, pose] = PaintTrain(false, pose);
            _train[1, pose] = PaintTrain(true, pose);
        }
        _lamp = Sprite.Glow(Math.Max(3, (int)(s.U * 0.008f)), Lit);
        _puffSmall = Sprite.Glow(Math.Max(2, (int)(s.U * 0.004f)), Smoke);
        _puffBig = Sprite.Glow(Math.Max(3, (int)(s.U * 0.008f)), Smoke);
    }

    /// <summary>
    /// Paints the whole train once, heading right (the locomotive at the right
    /// end): three carriages, then the cab, the round boiler and the funnel.
    /// "flip" mirrors the whole sheet for a train heading left.
    /// </summary>
    private Sprite PaintTrain(bool flip, int pose) => Sprite.Paint(_len, _h, g =>
    {
        if (flip) { g.TranslateTransform(_len, 0); g.ScaleTransform(-1, 1); }
        float L = _len, H = _h;
        float wr = Math.Max(1.2f, H * 0.17f);              // wheel radius
        float floor = H - wr;                              // the bottom of the bodies (wheels poke below)
        using var body = new SolidBrush(Body);
        using var lit = new SolidBrush(Lit);
        using var coupling = new Pen(Body, 1f);

        // Three carriages: plain boxes, each with three little lit windows.
        for (int i = 0; i < 3; i++)
        {
            float x0 = L * (0.00f + 0.24f * i), w = L * 0.22f;
            g.FillRectangle(body, x0, H * 0.25f, w, floor - H * 0.25f);
            for (int k = 0; k < 3; k++)
                g.FillRectangle(lit, x0 + w * (0.14f + 0.28f * k), H * 0.37f, Math.Max(1f, L * 0.035f), Math.Max(1f, H * 0.2f));
            g.DrawLine(coupling, x0 + w, floor - H * 0.2f, x0 + w + L * 0.02f, floor - H * 0.2f);
        }

        // The locomotive: a tall cab at the back, a round-nosed boiler, a funnel and a dome.
        g.FillRectangle(body, L * 0.72f, H * 0.14f, L * 0.10f, floor - H * 0.14f);                    // cab
        g.FillRectangle(lit, L * 0.745f, H * 0.28f, Math.Max(1f, L * 0.04f), Math.Max(1f, H * 0.2f)); // cab window
        g.FillRectangle(body, L * 0.82f, H * 0.38f, L * 0.15f, floor - H * 0.38f);                    // boiler
        g.FillEllipse(body, L * 0.91f, H * 0.38f, L * 0.09f, floor - H * 0.38f);                      // its round nose
        g.FillRectangle(body, L * 0.90f, H * 0.04f, L * 0.05f, H * 0.38f);                            // funnel
        g.FillRectangle(body, L * 0.88f, 0, L * 0.09f, Math.Max(1f, H * 0.12f));                      // its flared top
        g.FillEllipse(body, L * 0.80f, H * 0.28f, L * 0.05f, H * 0.16f);                              // dome

        // The headlight, a bright dot at the nose (the soft glow around it is stamped in Draw).
        using var lamp = new SolidBrush(Color.FromArgb(255, 250, 200));
        float lampR = Math.Max(1f, H * 0.1f);
        g.FillEllipse(lamp, L * 0.985f - lampR, H * 0.5f - lampR, lampR * 2, lampR * 2);

        // Wheels: a dark disc with one pale dot that sits a quarter turn further round on each pose.
        using var wheel = new SolidBrush(Color.FromArgb(14, 18, 40));
        using var dot = new SolidBrush(Color.FromArgb(215, 205, 160));
        float dotR = Math.Max(0.6f, wr * 0.3f);
        float turn = pose * MathF.Tau / Poses;
        void Wheel(float x)
        {
            g.FillEllipse(wheel, x - wr, floor - wr, wr * 2, wr * 2);                              // centre sits on the floor line, so the wheel touches the sheet's bottom
            g.FillEllipse(dot, x + MathF.Cos(turn) * wr * 0.55f - dotR, floor + MathF.Sin(turn) * wr * 0.55f - dotR, dotR * 2, dotR * 2);
        }
        for (int i = 0; i < 3; i++) { Wheel(L * (0.05f + 0.24f * i)); Wheel(L * (0.19f + 0.24f * i)); }
        foreach (float x in new[] { 0.76f, 0.86f, 0.95f }) Wheel(L * x);
    });

    public override void Begin(Random rng) => _left = rng.Next(2) == 0;

    /// <summary>Where the train's left edge is, t seconds in: it starts wholly off one side and ends wholly off the other.</summary>
    private float LeftEdge(float t)
    {
        float k = t / _trip, run = _s.Width + _len;
        return _left ? _s.Width - run * k : -_len + run * k;
    }

    public override void Draw(FrameBuffer fb, float t)
    {
        float wheelsY = _s.Horizon + _s.U * 0.008f;        // wheels sit just below the near mountains' feet, on the valley floor
        int top = (int)(wheelsY - _h);
        float x = LeftEdge(t);

        // The smoke first, so the train is stamped over it. Puff j left the funnel at j * PuffEvery.
        float funnelDX = _left ? _len - _funnelX : _funnelX;
        float rise = _s.U * 0.02f;
        for (int j = 0; j * PuffEvery < _trip; j++)
        {
            float age = t - j * PuffEvery;
            if (age < 0 || age > PuffLife) continue;
            float born = LeftEdge(j * PuffEvery) + funnelDX;            // the funnel's x at the moment of this puff
            float a = age / PuffLife;
            float px = born + (_left ? 1 : -1) * _s.U * 0.01f * a + _s.U * 0.002f * MathF.Sin(age * 3 + j);   // blown back a little, with a wobble
            float py = top - _s.U * 0.003f - rise * MathF.Sqrt(a);   // floats up, slowing as it goes
            (a < 0.4f ? _puffSmall : _puffBig).DrawCentered(fb, px, py, 0.85f * (1 - a) * Math.Min(1f, age * 4), _s.OpenValley);
        }

        int pose = (int)(t * 14) % Poses;
        _train[_left ? 1 : 0, pose].Draw(fb, (int)x, top, 1f, _s.OpenValley);

        // The headlight's soft glow, at the nose, which is the right end going right and the left end going left.
        float lampX = x + (_left ? _len * 0.015f : _len * 0.985f);
        _lamp.DrawCentered(fb, lampX, top + _h * 0.5f, 0.8f, _s.OpenValley);
    }
}
