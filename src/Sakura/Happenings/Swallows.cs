using System.Drawing.Drawing2D;
using Peckworks.Screensavers.Core;

namespace Sakura.Happenings;

/// <summary>
/// Tsubame: two or three swallows dart low over the lake, fast, swooping up
/// and down as they chase insects. One of them dips right down and touches the
/// water, leaving a tiny ring that spreads and fades.
///
/// The swallow is a sprite flip-book. A swallow is a very small bird that
/// moves very fast, so what matters is the silhouette: a dark navy back, a
/// rusty-red throat, a pale belly, long swept-back pointed wings and a deeply
/// forked tail. Its wings beat in short quick bursts (16 pages of a beat) and
/// then it glides with its wings held back (one more page), the way swallows
/// really fly.
///
/// A sprite cannot turn, so the swallow is painted at five climb angles (nose
/// down a lot, a little, level, up a little, up a lot), facing right and also
/// mirrored to face left. The path decides which page to stamp.
///
/// The ripple is drawn with thin ellipse lines (a ring seen at a shallow angle
/// is a flat oval), two of them, one just behind the other.
/// </summary>
internal sealed class Swallows : Happening
{
    private const int FlapPages = 16;
    private const int GlidePage = FlapPages;                // the 17th page: wings held back
    private const int Pages = FlapPages + 1;
    private const int Pitches = 5;                          // climb angles: -30, -15, 0, 15, 30 degrees
    private const float FlapHz = 5.4f;                      // beats per second while flapping
    private const float RippleLife = 1.3f;                  // seconds a ripple lasts
    private const float SpeedU = 0.45f;                     // across the screen, screen heights per second
    private const int MaxBirds = 3;

    private readonly Scenery _s;
    private readonly bool[] _stencil;
    private readonly Sprite[,,] _bird;                      // [direction (0 = right), pitch, page]
    private readonly Sprite _shadow;
    private readonly float _b;                              // the body length in pixels

    // This showing's dice (rolled in Begin)
    private int _n;
    private float _seconds = 6f;
    private readonly int[] _dir = new int[MaxBirds];
    private readonly float[] _start = new float[MaxBirds], _tc = new float[MaxBirds], _yc = new float[MaxBirds];
    private readonly float[] _amp = new float[MaxBirds], _period = new float[MaxBirds];
    private readonly float[] _psiX = new float[MaxBirds], _psiY = new float[MaxBirds], _beatOff = new float[MaxBirds];
    private int _diver;                                      // which bird touches the water
    private float _tauD, _yTouch, _xTouch;

    public override float Seconds => _seconds;
    public override int Layer => 1;                         // nearer than the boat

    public Swallows(Scenery s)
    {
        _s = s;
        _stencil = s.OpenBehindBanks;                       // behind the banks, trees and branches
        _b = Math.Max(8f, 0.02f * s.U);                     // body length; the wingspan comes out near 0.03 U

        _bird = new Sprite[2, Pitches, Pages];
        for (int d = 0; d < 2; d++)
            for (int p = 0; p < Pitches; p++)
                for (int g = 0; g < Pages; g++)
                    _bird[d, p, g] = PaintBird(_b, d == 1, -30f + 15f * p, g);

        // A faint soft shadow on the water under the bird, so it feels like it is skimming.
        int sw = (int)(_b * 1.8f) + 4, sh = Math.Max(3, (int)(_b * 0.45f) + 2);
        _shadow = Sprite.Paint(sw, sh, g =>
        {
            using var br = new SolidBrush(Color.FromArgb(70, 20, 30, 60));
            g.FillEllipse(br, 2, 1, sw - 4, sh - 2);
        });
    }

    /// <summary>One swallow, facing right (or mirrored), nose up by pitchDeg, on one page of the wing flip-book.</summary>
    private static Sprite PaintBird(float B, bool mirror, float pitchDeg, int page)
    {
        int box = (int)MathF.Ceiling(3.1f * B) + 2;
        // Wing angle for this page: a quick beat from raised to lowered and back, or held back for the glide.
        float alpha = page == GlidePage ? 0.18f : 0.18f + 0.85f * MathF.Sin(MathF.Tau * page / FlapPages);

        return Sprite.Paint(box, box, g =>
        {
            g.TranslateTransform(box / 2f, box / 2f);
            // Nose-up is a turn the other way round once the picture is mirrored.
            g.RotateTransform(mirror ? pitchDeg : -pitchDeg);
            if (mirror) g.ScaleTransform(-1, 1);
            g.ScaleTransform(B, B);                        // from here on, 1 unit = one body length

            Color navy = Color.FromArgb(20, 24, 48);
            Color wingNavy = Color.FromArgb(30, 38, 74);
            Color farNavy = Color.FromArgb(14, 17, 36);
            Color belly = Color.FromArgb(236, 230, 218);
            Color rust = Color.FromArgb(184, 78, 48);
            PointF P(float x, float y) => new(x, y);

            void Wing(Color c, float shoulderX, float a)
            {
                // The wing is a narrow curved blade swept back from the shoulder; a raised wing
                // points up and back, a lowered one points down and back.
                PointF A = P(shoulderX + 0.04f, -0.09f);
                PointF tip = P(shoulderX - 0.05f - 0.58f * MathF.Cos(a), -0.09f - 0.95f * MathF.Sin(a));
                PointF rear = P(shoulderX - 0.34f, -0.04f);
                // Which way is "forward" (the leading edge bows that way).
                float dx = tip.X - A.X, dy = tip.Y - A.Y, len = MathF.Sqrt(dx * dx + dy * dy) + 1e-4f;
                float nx = -dy / len, ny = dx / len;
                if (nx < 0) { nx = -nx; ny = -ny; }
                using var path = new GraphicsPath();
                path.AddBezier(A, P(A.X + dx * 0.33f + nx * 0.10f, A.Y + dy * 0.33f + ny * 0.10f),
                                  P(A.X + dx * 0.70f + nx * 0.07f, A.Y + dy * 0.70f + ny * 0.07f), tip);
                path.AddBezier(tip, P(tip.X + (rear.X - tip.X) * 0.35f + nx * 0.02f, tip.Y + (rear.Y - tip.Y) * 0.35f + ny * 0.02f),
                                    P(tip.X + (rear.X - tip.X) * 0.75f, tip.Y + (rear.Y - tip.Y) * 0.75f), rear);
                path.CloseFigure();
                using var br = new SolidBrush(c);
                g.FillPath(br, path);
            }

            // Far wing first (behind the body), a touch darker and a touch behind.
            Wing(farNavy, 0.02f, alpha * 0.9f);

            // The forked tail: two thin streamers, the deep fork a swallow is known for.
            using (var tailBr = new SolidBrush(navy))
            {
                g.FillPolygon(tailBr, [P(-0.38f, -0.03f), P(-1.08f, -0.14f), P(-0.66f, 0.01f), P(-0.40f, 0.05f)]);
                g.FillPolygon(tailBr, [P(-0.38f, 0.01f), P(-1.00f, 0.19f), P(-0.62f, 0.04f), P(-0.40f, 0.07f)]);
            }

            // The body: a navy teardrop with a pale belly along the bottom.
            using (var br = new SolidBrush(navy)) g.FillEllipse(br, -0.52f, -0.17f, 1.04f, 0.34f);
            using (var br = new SolidBrush(belly)) g.FillEllipse(br, -0.40f, 0.02f, 0.78f, 0.14f);
            // Rusty throat and forehead, then a tiny beak and a bright pinprick of an eye.
            using (var br = new SolidBrush(rust)) g.FillEllipse(br, 0.24f, -0.03f, 0.27f, 0.17f);
            using (var br = new SolidBrush(navy)) g.FillPolygon(br, [P(0.48f, -0.04f), P(0.60f, 0.0f), P(0.48f, 0.04f)]);
            using (var br = new SolidBrush(Color.FromArgb(235, 255, 255, 255))) g.FillEllipse(br, 0.30f, -0.075f, 0.055f, 0.055f);

            // Near wing, in front.
            Wing(wingNavy, 0.10f, alpha);
        });
    }

    public override void Begin(Random rng)
    {
        float u = _s.U, W = _s.Width, H = _s.Height;
        _n = rng.Next(2, 4);                                  // two or three swallows
        float yMin = _s.HorizonY + 0.04f * u;
        float yMax = H - (H - _s.HorizonY) / 3f;              // not lower than the bottom third of the lake
        float range = Math.Max(yMax - yMin, 0.02f * u);
        float margin = 0.1f * u + 1.6f * _b;
        float crossing = (W + 2 * margin) / (SpeedU * u);     // seconds to cross the screen

        _diver = rng.Next(_n);
        float end = 0;
        for (int i = 0; i < _n; i++)
        {
            _dir[i] = rng.Next(2);                            // each its own way, so they cross paths
            _start[i] = i * 1.0f + 0.5f * rng.NextSingle();
            _tc[i] = crossing * (0.9f + 0.2f * rng.NextSingle());
            _amp[i] = Math.Min(0.03f * u, range * 0.3f) * (0.6f + 0.4f * rng.NextSingle());
            _period[i] = 1.6f + 1.2f * rng.NextSingle();
            _yc[i] = yMin + _amp[i] + (range - 2 * _amp[i]) * rng.NextSingle() * (i == _diver ? 0.5f : 1f);
            _psiX[i] = rng.NextSingle() * MathF.Tau;
            _psiY[i] = rng.NextSingle() * MathF.Tau;
            _beatOff[i] = rng.NextSingle();
            end = Math.Max(end, _start[i] + _tc[i]);
        }

        // The diver: pick a moment around the middle of its crossing where it is over open water
        // (not behind a bank), and make the path pass through the water's surface there.
        _yTouch = Math.Max(yMin + range * (0.55f + 0.4f * rng.NextSingle()), _yc[_diver] + 0.015f * u);
        _yTouch = Math.Min(_yTouch, yMax);
        _tauD = _tc[_diver] * 0.5f;
        for (int tries = 0; tries < 14; tries++)
        {
            float cand = _tc[_diver] * (0.3f + 0.4f * rng.NextSingle());
            float cx = X(_diver, cand);
            int px = (int)Math.Clamp(cx, 0, W - 1), py = (int)Math.Clamp(_yTouch + 0.5f * _b, 0, H - 1);
            _tauD = cand;
            if (_stencil[py * _s.Width + px]) break;          // open water here: good
        }
        _xTouch = X(_diver, _tauD);

        // Long enough for the last bird to leave, and for the ripple to finish.
        _seconds = Math.Max(end, _start[_diver] + _tauD + RippleLife + 0.3f) + 0.2f;
    }

    // ---- the paths (pure formulas of the bird's own time tau) ----

    private float X(int i, float tau)
    {
        float margin = 0.1f * _s.U + 1.6f * _b;
        float p = tau / _tc[i];
        float x0 = _dir[i] == 0 ? -margin : _s.Width + margin;
        float sgn = _dir[i] == 0 ? 1f : -1f;
        return x0 + sgn * (_s.Width + 2 * margin) * (p + 0.03f * MathF.Sin(4 * MathF.PI * p + _psiX[i]));   // a little surge and ease
    }

    private float Y(int i, float tau)
    {
        float y = _yc[i] + _amp[i] * MathF.Sin(MathF.Tau * tau / _period[i] + _psiY[i]);                   // the swoops
        if (i == _diver)
        {
            // A smooth dip that reaches exactly the touch height at _tauD, then lifts away.
            float k = (tau - _tauD) / 0.28f;
            y += (_yTouch - y) * MathF.Exp(-k * k);
        }
        return y;
    }

    public override void Draw(FrameBuffer fb, float t)
    {
        float u = _s.U;
        for (int i = 0; i < _n; i++)
        {
            float tau = t - _start[i];
            if (tau < 0 || tau > _tc[i]) continue;
            float x = X(i, tau), y = Y(i, tau);

            // Which way is it heading (for the climb angle)? Ask the path a moment either side.
            const float e = 0.04f;
            float vx = (X(i, tau + e) - X(i, tau - e)) / (2 * e), vy = (Y(i, tau + e) - Y(i, tau - e)) / (2 * e);
            float pitch = Math.Clamp(MathF.Atan2(-vy, MathF.Abs(vx) + 1e-3f) * 180f / MathF.PI, -30f, 30f);
            int pi = Math.Clamp((int)MathF.Round((pitch + 30f) / 15f), 0, Pitches - 1);

            // Flap in bursts of three beats, then glide: a beat cycle ends where the glide pose begins.
            float flapLen = 3f / FlapHz, glideLen = 0.45f + 0.15f * (i % 2);
            float local = (tau + _beatOff[i] * (flapLen + glideLen)) % (flapLen + glideLen);
            int page = local < flapLen ? (int)(local * FlapHz * FlapPages) % FlapPages : GlidePage;

            // The shadow on the water first (it is lower and behind the bird).
            _shadow.DrawCentered(fb, x, y + 0.85f * _b, 0.5f, _stencil);
            _bird[_dir[i], pi, page].DrawCentered(fb, x, y, 1f, _stencil);
        }

        // ---- the ripple left where the diver touched the water ----
        float age = t - _start[_diver] - _tauD;
        for (int ring = 0; ring < 2; ring++)
        {
            float a = age - 0.22f * ring;
            if (a < 0 || a > RippleLife) continue;
            float f = a / RippleLife;
            float rx = Math.Max(2f, 0.034f * u * MathF.Sqrt(f)) + 0.002f * u;     // spreads fast at first, then slows
            float ry = rx * 0.30f;                                                  // a flat oval: seen at a shallow angle
            float alpha = 0.6f * (1 - f) * (1 - f);
            float cx = _xTouch, cy = _yTouch + 0.55f * _b;
            PointF prev = new(cx + rx, cy);
            for (int k = 1; k <= 28; k++)
            {
                float ang = MathF.Tau * k / 28f;
                var p = new PointF(cx + rx * MathF.Cos(ang), cy + ry * MathF.Sin(ang));
                fb.Line(prev, p, Color.FromArgb(236, 246, 255), alpha, Math.Max(1f, 0.0013f * u), _stencil);
                prev = p;
            }
        }
    }
}
