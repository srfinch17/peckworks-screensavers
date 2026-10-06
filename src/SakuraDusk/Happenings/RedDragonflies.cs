using System.Drawing.Drawing2D;
using Peckworks.Screensavers.Core;

namespace SakuraDusk.Happenings;

/// <summary>
/// (Body length: 0.03 U.)
/// Akatombo, the red dragonflies of a Japanese late-summer and autumn evening (and the title
/// of a famous lullaby-like song about them). Three to five of them dart and
/// hover over the pond and around the bridge. A dragonfly does not drift: it
/// flicks in a straight line, stops dead, hangs in the air with its wings a
/// blur, then flicks somewhere else. And one of them lands on the bridge's top
/// rail, folds into stillness for a couple of seconds, wings out flat, and
/// then is gone again.
///
/// HOW A DRAGONFLY'S DAY IS PLANNED. In Begin, each one is given a list of
/// stops ("waypoints"): where it is, how long it stays there, then a quick
/// dart to the next. For every stop we write down two times: when it ARRIVES
/// and when it DEPARTS. Draw then only asks "what is t?": before the departure
/// time it hangs at that stop; between a departure and the next arrival it is
/// darting; and so on. (Nothing is moved a little each frame, so any moment
/// can be drawn alone.)
///
/// THE WINGS ARE A FLIP-BOOK. Real dragonfly wings beat far faster than the
/// screen can show, so the picture cheats the way a photo of a fan does: eight
/// poses of the four wings, cycled quickly, and in each pose a faint fan-shaped
/// smear showing where the wings have just been. Forewings and hindwings beat
/// opposite to each other, as the real ones do. The sprites also come in two
/// facings (left and right) and three pitches (nose up, level, nose down), since
/// we cannot turn a sprite while stamping it. A perched one has its own pair,
/// wings flat and legs holding the rail.
/// </summary>
internal sealed class RedDragonflies : Happening
{
    private const int MaxFlies = 5, MaxStops = 24, Poses = 8;
    private const float Reach = 0.25f;                 // legs reach this far (in body lengths) below the body's middle

    private readonly DuskScenery _s;
    private readonly float _b;                         // body length in pixels (0.025 U, never under 9 px)
    private readonly int _size;
    private readonly Sprite[][][] _fly = new Sprite[2][][];   // [facing 0 right, 1 left][pitch 0 up, 1 level, 2 down][wing pose]
    private readonly Sprite[] _perched = new Sprite[2];       // [facing]

    // This showing's plan (rolled in Begin).
    private int _count;
    private readonly float[] _start = new float[MaxFlies];                 // when each first appears
    private readonly int[] _stops = new int[MaxFlies];                     // how many stops each has
    private readonly float[,] _px = new float[MaxFlies, MaxStops], _py = new float[MaxFlies, MaxStops];
    private readonly float[,] _arrive = new float[MaxFlies, MaxStops], _depart = new float[MaxFlies, MaxStops];
    private readonly int[,] _face = new int[MaxFlies, MaxStops];           // 0 = facing right, 1 = facing left (when it got to the stop)
    private readonly int[,] _pitch = new int[MaxFlies, MaxStops];          // pitch of the dart that LEAVES this stop
    private readonly bool[,] _rests = new bool[MaxFlies, MaxStops];        // is it perched on the rail at this stop?
    private readonly float[] _wobble = new float[MaxFlies];

    public override float Seconds => 10f;
    public override int Layer => 1;                    // near the viewer: over the water, bridge and banks, under the petals
    public override string? Claims => "bridge";        // one lands on the rail, so keep clear of anything else that uses the bridge

    public RedDragonflies(DuskScenery s)
    {
        _s = s;
        _b = Math.Max(9f, s.U * 0.03f);   // a touch over the 0.025 U brief: a 25 px speck at 1080p is easy to miss
        _size = (int)(_b * 1.7f) + 4;
        float[] pitches = [-22f, 0f, 22f];
        for (int f = 0; f < 2; f++)
        {
            _fly[f] = new Sprite[3][];
            for (int p = 0; p < 3; p++)
            {
                _fly[f][p] = new Sprite[Poses];
                for (int k = 0; k < Poses; k++)
                {
                    int facing = f, pitch = p, pose = k;
                    _fly[f][p][k] = Sprite.Paint(_size, _size, g => PaintFly(g, facing == 1, pitches[pitch], pose));
                }
            }
            int fc = f;
            _perched[f] = Sprite.Paint(_size, _size, g => PaintFly(g, fc == 1, -8f, -1));
        }
    }

    // ---------------------------------------------------------------- the painting

    /// <summary>
    /// One dragonfly facing right (or mirrored), pitched by pitchDeg
    /// (negative = nose up). pose 0 to 7 = a wing pose in flight; -1 = perched.
    /// Everything is drawn in "body lengths": the canvas is scaled so that 1.0
    /// is the whole body, then x runs forward and y runs down.
    /// </summary>
    private void PaintFly(Graphics g, bool mirror, float pitchDeg, int pose)
    {
        g.TranslateTransform(_size / 2f, _size / 2f);
        if (mirror) g.ScaleTransform(-1, 1);
        g.RotateTransform(pitchDeg);
        g.ScaleTransform(_b, _b);
        float px(float pixels) => pixels / _b;         // a pixel width, converted into body lengths (pens are measured in the scaled space)

        Color outline = Color.FromArgb(120, 30, 32);
        using var edge = new Pen(outline, Math.Max(px(0.8f), 0.016f)) { LineJoin = LineJoin.Round };

        // ---- the wings, FAR pair first (they are behind the body, a little fainter) ----
        bool perched = pose < 0;
        float flap = perched ? 0 : pose * MathF.Tau / Poses;
        // Angles are measured up from straight ahead (counterclockwise); the wing at 90 stands straight up.
        float foreNear = perched ? 172 : 100 + 38 * MathF.Sin(flap);
        float hindNear = perched ? 188 : 100 + 38 * MathF.Sin(flap + MathF.PI);
        float foreFar = perched ? 160 : foreNear + 12, hindFar = perched ? 200 : hindNear + 12;
        if (!perched) { Fan(g, 0.14f, -0.075f, 0.56f, 62, 138, 18); Fan(g, 0.06f, -0.075f, 0.50f, 62, 138, 18); }
        Wing(g, 0.14f, -0.075f, 0.56f, foreFar, 0.6f, px(0.7f));
        Wing(g, 0.06f, -0.075f, 0.50f, hindFar, 0.6f, px(0.7f));

        // ---- legs: three short pairs; long and gripping when perched, tucked when flying ----
        using (var leg = new Pen(Color.FromArgb(150, 80, 28, 30), Math.Max(px(0.8f), 0.014f)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
        {
            float drop = perched ? Reach : 0.12f;
            float[] at = [0.18f, 0.10f, 0.02f];
            for (int i = 0; i < 3; i++)
            {
                float splay = perched ? (i - 1) * 0.05f : 0.03f;
                g.DrawLine(leg, at[i], 0.06f, at[i] + splay, drop);
            }
        }

        // ---- the abdomen: the long red tail, tapering, with a few darker rings and a warm sunset glint along its back ----
        PointF[] tail =
        [
            new(0.02f, -0.048f), new(-0.20f, -0.036f), new(-0.52f, -0.020f),
            new(-0.54f, 0f), new(-0.52f, 0.020f), new(-0.20f, 0.036f), new(0.02f, 0.048f),
        ];
        using (var body = new SolidBrush(Color.FromArgb(236, 76, 40)))
            g.FillPolygon(body, tail);
        g.DrawPolygon(edge, tail);
        using (var ring = new Pen(Color.FromArgb(90, 120, 30, 30), Math.Max(px(0.7f), 0.012f)))
            for (int i = 0; i < 5; i++)
            {
                float x = -0.10f - i * 0.085f, hh = 0.04f - i * 0.004f;
                g.DrawLine(ring, x, -hh, x, hh);
            }
        using (var glint = new Pen(Color.FromArgb(170, 255, 170, 100), Math.Max(px(0.9f), 0.014f)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            g.DrawLine(glint, 0.0f, -0.034f, -0.46f, -0.016f);

        // ---- thorax (the chest the wings grow from) and head with its big eye ----
        using (var chest = new SolidBrush(Color.FromArgb(208, 52, 38)))
            g.FillEllipse(chest, 0.10f - 0.13f, -0.01f - 0.095f, 0.26f, 0.19f);
        g.DrawEllipse(edge, 0.10f - 0.13f, -0.01f - 0.095f, 0.26f, 0.19f);
        using (var head = new SolidBrush(Color.FromArgb(228, 84, 48)))
            g.FillEllipse(head, 0.31f - 0.095f, -0.095f, 0.19f, 0.19f);
        g.DrawEllipse(edge, 0.31f - 0.095f, -0.095f, 0.19f, 0.19f);
        using (var eye = new SolidBrush(Color.FromArgb(54, 16, 24)))
            g.FillEllipse(eye, 0.335f - 0.07f, -0.015f - 0.07f, 0.14f, 0.14f);          // a big, round, dark eye
        using (var shine = new SolidBrush(Color.FromArgb(240, 255, 250, 240)))
        {
            float dot = Math.Max(0.022f, px(0.9f));
            g.FillEllipse(shine, 0.355f - dot, -0.045f - dot, dot * 2, dot * 2);       // its tiny white highlight
        }

        // ---- the NEAR wings, over the body ----
        Wing(g, 0.14f, -0.075f, 0.56f, foreNear, 1f, px(0.7f));
        Wing(g, 0.06f, -0.075f, 0.50f, hindNear, 1f, px(0.7f));
    }

    /// <summary>
    /// One clear wing: a long leaf-shaped pane, very see-through, with a pale
    /// rim, a darker vein down the middle and a couple across, and a faint warm
    /// shine near its root. It is drawn along +x from its root, then turned to angle.
    /// </summary>
    private static void Wing(Graphics g, float rootX, float rootY, float len, float angleDeg, float strength, float rimPx)
    {
        var state = g.Save();
        g.TranslateTransform(rootX, rootY);
        g.RotateTransform(-angleDeg);                   // counterclockwise on screen is negative in this kit
        float half = len * 0.12f;
        using var path = new GraphicsPath();
        path.AddEllipse(0, -half, len, half * 2);
        using (var glass = new SolidBrush(Color.FromArgb((int)(100 * strength), 255, 238, 214)))
            g.FillPath(glass, path);
        using (var shine = new SolidBrush(Color.FromArgb((int)(70 * strength), 255, 196, 140)))
            g.FillEllipse(shine, len * 0.05f, -half * 0.8f, len * 0.4f, half * 0.9f);   // faint warm sunset shine
        using (var rim = new Pen(Color.FromArgb((int)(185 * strength), 255, 214, 160), Math.Max(rimPx, 0.012f)))
            g.DrawPath(rim, path);
        using (var vein = new Pen(Color.FromArgb((int)(150 * strength), 96, 44, 40), Math.Max(rimPx * 0.8f, 0.01f)))
        {
            g.DrawLine(vein, len * 0.04f, 0, len * 0.94f, 0);
            for (int i = 1; i <= 3; i++)
                g.DrawLine(vein, len * (0.18f + 0.2f * i), -half * 0.75f, len * (0.18f + 0.2f * i), half * 0.75f);
        }
        g.Restore(state);
    }

    /// <summary>The blur: a faint fan showing the whole sweep the wing has just made.</summary>
    private static void Fan(Graphics g, float rootX, float rootY, float len, float fromDeg, float toDeg, int alpha)
    {
        using var smear = new SolidBrush(Color.FromArgb(alpha, 255, 226, 190));
        g.FillPie(smear, rootX - len, rootY - len, len * 2, len * 2, -toDeg, toDeg - fromDeg);
    }

    // ---------------------------------------------------------------- the plan

    private PointF Zone(Random rng, float lo, float hi, float loY, float hiY) =>
        new(_s.Width * (lo + (hi - lo) * (float)rng.NextDouble()), loY + (hiY - loY) * (float)rng.NextDouble());

    public override void Begin(Random rng)
    {
        float u = _s.U, w = _s.Width;
        // Where they may roam: across the middle of the pond, from a little above the bridge's peak down onto the near water.
        float xLo = 0.28f, xHi = 0.72f;
        float yLo = Math.Max(_s.Height * 0.08f, _s.Bridge.Top - 0.12f * u);
        float yHi = Math.Min(_s.Height * 0.93f, _s.Horizon + 0.22f * u);
        float R() => (float)rng.NextDouble();

        _count = rng.Next(3, MaxFlies + 1);
        for (int f = 0; f < _count; f++)
        {
            float tc = f == 0 ? 0.2f : 0.1f + 1.3f * R();
            _start[f] = tc;
            _wobble[f] = R() * MathF.Tau;
            PointF p = Zone(rng, xLo, xHi, yLo, yHi);
            int k = 0;
            _px[f, 0] = p.X; _py[f, 0] = p.Y;
            _arrive[f, 0] = tc;
            _face[f, 0] = rng.Next(2);
            _rests[f, 0] = false;
            int move = 0;
            while (k < MaxStops - 1)
            {
                // Stay here a while (a long while if resting on the rail), then dart off.
                float hold = _rests[f, k] ? 2.0f + 0.6f * R() : 0.35f + 0.75f * R();
                _depart[f, k] = tc + hold;
                tc += hold;
                if (tc > Seconds + 0.5f) break;

                bool perchNext = f == 0 && move == 2;
                PointF q;
                if (perchNext)
                {
                    // The top rail somewhere along the middle of the bridge; the body sits so its legs just touch it.
                    PointF rail = _s.Bridge.RailAt(0.32f + 0.36f * R());
                    q = new PointF(rail.X, rail.Y - Reach * _b);
                }
                else
                {
                    q = p;
                    for (int tries = 0; tries < 8; tries++)
                    {
                        float a = R() * MathF.Tau, d = u * (0.08f + 0.22f * R());
                        var c = new PointF(p.X + MathF.Cos(a) * d, p.Y + MathF.Sin(a) * d * 0.6f);   // flatter than tall: they skim
                        if (c.X > w * xLo && c.X < w * xHi && c.Y > yLo && c.Y < yHi) { q = c; break; }
                    }
                    if (q == p) q = Zone(rng, xLo, xHi, yLo, yHi);
                }

                float dx = q.X - p.X, dy = q.Y - p.Y, dist = MathF.Sqrt(dx * dx + dy * dy);
                float dur = Math.Clamp(dist / (u * (0.55f + 0.35f * R())), 0.18f, 0.5f);   // quick: about half a screen height a second
                _pitch[f, k] = Math.Abs(dy) < 0.35f * dist ? 1 : dy < 0 ? 0 : 2;
                tc += dur;
                k++;
                _px[f, k] = q.X; _py[f, k] = q.Y;
                _arrive[f, k] = tc;
                _face[f, k] = Math.Abs(dx) < 1 ? _face[f, k - 1] : dx > 0 ? 0 : 1;
                _rests[f, k] = perchNext;
                p = q;
                move++;
            }
            _depart[f, k] = float.MaxValue;
            _stops[f] = k + 1;
        }
    }

    // ---------------------------------------------------------------- the showing

    public override void Draw(FrameBuffer fb, float t)
    {
        float u = _s.U;
        for (int f = 0; f < _count; f++)
        {
            float alpha = Smooth((t - _start[f]) / 0.5f) * Smooth((Seconds - t) / 0.7f);
            if (alpha <= 0.01f) continue;

            // Which part of its plan is it in?
            int k = 0;
            while (k < _stops[f] - 1 && t >= _depart[f, k] && t >= _arrive[f, k + 1]) k++;
            float x, y;
            int facing = _face[f, k], pitch = 1;
            bool darting = k < _stops[f] - 1 && t >= _depart[f, k];
            if (darting)
            {
                // Between stop k and stop k+1. Quick off the mark, then a hard brake (an ease-out): a dart, not a glide.
                float span = _arrive[f, k + 1] - _depart[f, k];
                float e = Math.Clamp((t - _depart[f, k]) / Math.Max(0.01f, span), 0f, 1f);
                e = 1 - MathF.Pow(1 - e, 2.4f);
                x = _px[f, k] + (_px[f, k + 1] - _px[f, k]) * e;
                y = _py[f, k] + (_py[f, k + 1] - _py[f, k]) * e;
                facing = _face[f, k + 1];
                pitch = _pitch[f, k];
            }
            else
            {
                x = _px[f, k];
                y = _py[f, k];
                if (!_rests[f, k])
                {
                    // Hovering is not perfectly still: a tiny quick wobble, never under about a pixel.
                    float amp = Math.Max(1.2f, u * 0.003f);
                    x += amp * MathF.Sin(t * 4.1f + _wobble[f]);
                    y += amp * MathF.Sin(t * 5.3f + _wobble[f] * 1.6f);
                }
            }
            x = MathF.Round(x);
            y = MathF.Round(y);

            Sprite sprite = !darting && _rests[f, k]
                ? _perched[facing]
                : _fly[facing][pitch][((int)(t * 50f) + f * 3) % Poses];   // about 50 pictures a second: a blur
            sprite.DrawCentered(fb, x, y, alpha);
        }
    }
}
