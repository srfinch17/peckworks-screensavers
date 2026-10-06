using System.Drawing.Drawing2D;
using Peckworks.Screensavers.Core;

namespace Christmas.Happenings;

/// <summary>
/// A snowman builds itself on the snow, the way a child would: a big
/// snowball drops in and lands with a little bounce and a puff of snow, a
/// medium one lands on top of it, a small one (the head) on top of that.
/// Then the face pops on piece by piece (two coal eyes, a carrot nose, a row
/// of coal-dot smile), two stick arms, a red scarf and a black top hat. Then
/// it waves one arm for a few seconds, and fades away gently.
///
/// Everything is worked out from t. Each piece has a start time (a plain
/// list in Begin), and "how old is this piece" is just t minus that time.
///
///   - A ball DROPS: its height above its resting place shrinks with the
///     square of the time (gravity), then it BOUNCES: a short sine hump that
///     dies away.
///   - A face piece POPS: it is painted ahead of time at four sizes (small,
///     bigger, a little too big, just right) and we stamp one after another.
///     That is a "flip-book" of sizes, and it reads as a tiny boing.
///   - The waving arm is a thick line (fb.Line) whose angle swings on a sine
///     wave. The wave eases in and out, so the arm does not jerk up.
///
/// The balls are white, so on white snow they would vanish. Each one is
/// shaded grey-blue on the side away from the moon (lower right) and has a
/// soft shadow on the snow, like a pencil outline round a white crayon.
///
/// It stands on the snow at a spot that is clear of the cabin and every pine.
/// It claims "snow" so it never shares the snow with another happening.
/// </summary>
internal sealed class Snowman : Happening
{
    private const float Life = 22f;
    private const float DropTime = 0.55f;      // how long a ball takes to fall
    private const int Puffs = 8;               // snow dots thrown up per landing

    // When each part starts, in seconds from the beginning of the showing.
    private static readonly float[] BallStart = [0.3f, 1.5f, 2.7f];
    private const float EyeL = 4.0f, EyeR = 4.25f, NoseAt = 4.5f, SmileAt = 4.9f, SmileGap = 0.12f;
    private const float ArmL = 6.2f, ArmR = 6.5f, ScarfAt = 6.9f, HatAt = 7.4f;
    private const float WaveFrom = 8.4f, WaveTo = 14.5f;
    private const int SmileDots = 5;

    private static readonly Color Coal = Color.FromArgb(30, 30, 38);
    private static readonly Color Stick = Color.FromArgb(112, 76, 48);

    private readonly ChristmasScenery _s;
    private readonly float _bigR, _medR, _headR;
    private readonly Sprite _big, _med, _head, _shadow, _dot;
    private readonly Sprite[] _eye, _smile, _nose, _scarf, _hat;
    private float _x, _foot;                                    // where it stands this showing
    private readonly float[] _puffVx = new float[3 * Puffs];    // each snow dot's sideways speed, pixels per second
    private readonly float[] _puffVy = new float[3 * Puffs];    // and how hard it is thrown up

    public override float Seconds => Life;
    public override string? Claims => "snow";

    public Snowman(ChristmasScenery s)
    {
        _s = s;
        float u = s.U;
        _bigR = Math.Max(5f, u * 0.027f);
        _medR = Math.Max(4f, u * 0.020f);
        _headR = Math.Max(3f, u * 0.0155f);
        _big = Ball(_bigR);
        _med = Ball(_medR);
        _head = Ball(_headR);
        _dot = Sprite.Paint(7, 7, g =>
        {
            using var rim = new SolidBrush(Color.FromArgb(200, 168, 186, 226));
            using var core = new SolidBrush(Color.White);
            g.FillEllipse(rim, 0.5f, 0.5f, 6, 6);
            g.FillEllipse(core, 1.5f, 1.5f, 4, 4);
        });

        // The shadow on the snow: a flat soft ellipse, darkest in the middle.
        int sw = (int)(_bigR * 3.2f) + 2, sh = (int)(_bigR * 0.9f) + 2;
        _shadow = Sprite.Paint(sw, sh, g =>
        {
            using var path = new GraphicsPath();
            path.AddEllipse(1, 1, sw - 2, sh - 2);
            using var br = new PathGradientBrush(path)
            {
                CenterColor = Color.FromArgb(150, 70, 96, 156),
                SurroundColors = [Color.FromArgb(0, 70, 96, 156)],
                Blend = Sprite.SoftFalloff,
            };
            g.FillPath(br, path);
        });

        // ---- the little pieces, each painted at four sizes for its pop ----
        float h = _headR;
        int eyeSize = (int)(h * 0.5f) + 6;
        float er = Math.Max(1.3f, h * 0.13f);
        _eye = Pops(eyeSize, (g, c) => Dot(g, c, c, er, Coal));
        float sr = Math.Max(1.1f, h * 0.08f);
        _smile = Pops((int)(sr * 2) + 6, (g, c) => Dot(g, c, c, sr, Coal));

        int noseSize = (int)(h * 1.0f) + 8;
        _nose = Pops(noseSize, (g, c) =>
        {
            // A carrot pointing to the viewer's right: wide at the face, thin at the tip.
            float len = h * 0.75f, wide = Math.Max(2f, h * 0.24f);
            using var carrot = new SolidBrush(Color.FromArgb(246, 134, 36));
            g.FillPolygon(carrot, [new PointF(c - len / 2, c - wide / 2), new PointF(c + len / 2, c + wide * 0.15f), new PointF(c - len / 2, c + wide / 2)]);
        });

        int scarfSize = (int)(h * 3.0f) + 8;
        _scarf = Pops(scarfSize, (g, c) =>
        {
            // A red band round the neck, with a tail hanging down the right side.
            using var red = new SolidBrush(Color.FromArgb(208, 44, 58));
            using var dark = new SolidBrush(Color.FromArgb(150, 120, 24, 40));
            float bw = h * 1.9f, bh = Math.Max(3f, h * 0.45f);
            g.FillRectangle(red, c + h * 0.35f, c, Math.Max(2.5f, h * 0.38f), h * 1.05f);   // the hanging tail
            g.FillRectangle(dark, c + h * 0.35f, c + h * 0.95f, Math.Max(2.5f, h * 0.38f), Math.Max(1f, h * 0.1f));
            g.FillEllipse(red, c - bw / 2, c - bh / 2, bw, bh);
            using var shade = new Pen(Color.FromArgb(150, 120, 24, 40), Math.Max(1f, h * 0.07f));
            g.DrawArc(shade, c - bw / 2, c - bh / 2, bw, bh, 10, 160);                       // the lower edge, a touch darker
        });

        int hatSize = (int)(h * 3.0f) + 8;
        _hat = Pops(hatSize, (g, c) =>
        {
            // Centered on the brim. A pale edge keeps the black hat readable against the dark sky.
            using var black = new SolidBrush(Color.FromArgb(30, 26, 38));
            using var edge = new Pen(Color.FromArgb(140, 150, 160, 205), Math.Max(1f, h * 0.06f));
            using var band = new SolidBrush(Color.FromArgb(200, 44, 58));
            float bw = h * 2.0f, bh = Math.Max(3f, h * 0.38f), cw = h * 1.2f, ch = h * 1.0f;
            g.FillRectangle(black, c - cw / 2, c - ch, cw, ch);
            g.FillRectangle(band, c - cw / 2, c - h * 0.36f, cw, h * 0.26f);
            g.DrawLine(edge, c - cw / 2, c - ch, c - cw / 2, c);
            g.DrawLine(edge, c + cw / 2, c - ch, c + cw / 2, c);
            g.DrawLine(edge, c - cw / 2, c - ch, c + cw / 2, c - ch);
            g.FillEllipse(black, c - bw / 2, c - bh / 2, bw, bh);
            g.DrawEllipse(edge, c - bw / 2, c - bh / 2, bw, bh);
        });
    }

    /// <summary>Four copies of a small picture at pop sizes (small, bigger, a bit too big, right), all about the same center.</summary>
    private static Sprite[] Pops(int size, Action<Graphics, float> paint)
    {
        float[] k = [0.35f, 0.75f, 1.15f, 1f];
        return [.. k.Select(f => Sprite.Paint(size, size, g =>
        {
            g.TranslateTransform(size / 2f, size / 2f);
            g.ScaleTransform(f, f);
            g.TranslateTransform(-size / 2f, -size / 2f);
            paint(g, size / 2f);
        }))];
    }

    private static Sprite Pick(Sprite[] pops, float age) => pops[age < 0.08f ? 0 : age < 0.16f ? 1 : age < 0.26f ? 2 : 3];

    private static void Dot(Graphics g, float x, float y, float r, Color c)
    {
        using var b = new SolidBrush(c);
        g.FillEllipse(b, x - r, y - r, r * 2, r * 2);
    }

    /// <summary>
    /// One snowball: grey-blue all over, then a big soft white highlight laid
    /// over the upper left (the moon side), so the lower right is left shaded.
    /// </summary>
    private static Sprite Ball(float r)
    {
        int pad = 3, d = (int)MathF.Ceiling(r * 2) + pad * 2;
        return Sprite.Paint(d, d, g =>
        {
            using var path = new GraphicsPath();
            path.AddEllipse(pad, pad, r * 2, r * 2);
            using (var shade = new SolidBrush(Color.FromArgb(255, 172, 192, 228)))
                g.FillPath(shade, path);

            var state = g.Save();
            g.SetClip(path, CombineMode.Intersect);
            using var hi = new GraphicsPath();
            float hr = r * 1.15f;
            hi.AddEllipse(pad + r - r * 0.25f - hr, pad + r - r * 0.25f - hr, hr * 2, hr * 2);
            using (var glow = new PathGradientBrush(hi)
            {
                CenterColor = Color.White,
                SurroundColors = [Color.FromArgb(0, 255, 255, 255)],
                Blend = Sprite.SoftFalloff,
            })
                g.FillPath(glow, hi);
            g.Restore(state);

            using var rim = new Pen(Color.FromArgb(150, 132, 156, 206), Math.Max(1f, r * 0.06f));
            g.DrawPath(rim, path);
        });
    }

    public override void Begin(Random rng)
    {
        float w = _s.Width, u = _s.U, cabinHalf = _s.CabinWall.Width / 2;

        // Try random spots until one is at least 0.09 U from the cabin and every pine trunk
        // (the brief asked for 0.07; the pines' lowest branches reach out wider than that).
        float bestClear = float.MinValue, bestX = w * 0.5f;
        for (int i = 0; i < 80; i++)
        {
            float x = w * (0.12f + 0.76f * (float)rng.NextDouble());
            float clear = Math.Abs(x - _s.CabinFoot.X) - cabinHalf;
            foreach (var p in _s.Pines) clear = Math.Min(clear, Math.Abs(x - p.X));
            if (clear > bestClear) { bestClear = clear; bestX = x; }
            if (clear >= u * 0.09f) break;
        }
        _x = bestX;
        _foot = _s.Ground.YAt(_x);

        for (int i = 0; i < _puffVx.Length; i++)
        {
            _puffVx[i] = u * 0.06f * ((float)rng.NextDouble() * 2 - 1);
            _puffVy[i] = u * (0.02f + 0.05f * (float)rng.NextDouble());
        }
    }

    // ---- where each ball rests (its center), measured from the foot ----
    private float BigCy => _foot - _bigR * 0.92f;
    private float MedCy => BigCy - 0.80f * (_bigR + _medR);
    private float HeadCy => MedCy - 0.80f * (_medR + _headR);

    public override void Draw(FrameBuffer fb, float t)
    {
        float u = _s.U;
        float fade = Smooth((Life - t) / 2.5f);                          // melts away gently at the end
        float[] rest = [BigCy, MedCy, HeadCy];
        float[] rad = [_bigR, _medR, _headR];
        Sprite[] balls = [_big, _med, _head];

        // The shadow grows as the big ball comes down, so it looks like it is really about to land.
        float sk = Smooth((t - BallStart[0]) / DropTime);
        if (sk > 0)
            _shadow.DrawCentered(fb, _x + _bigR * 0.35f, _foot, fade * sk);

        for (int i = 0; i < 3; i++)
        {
            float a = t - BallStart[i];
            if (a < 0) continue;
            float y = rest[i];
            if (a < DropTime)
            {
                float k = a / DropTime;
                y -= u * 0.30f * (1 - k * k);                            // falling: far above at first, faster and faster
            }
            else
            {
                float b = (a - DropTime) / 0.35f;                        // the bounce: one small hump that dies away
                if (b < 1) y -= rad[i] * 0.22f * MathF.Abs(MathF.Sin(b * MathF.PI)) * (1 - b);
            }
            balls[i].DrawCentered(fb, _x, y, fade);
        }

        // The puffs of snow thrown up at each landing.
        for (int i = 0; i < 3; i++)
        {
            float b = t - BallStart[i] - DropTime;
            if (b < 0 || b > 0.7f) continue;
            float baseY = rest[i] + rad[i] * 0.9f;                       // the ball's underside
            for (int j = 0; j < Puffs; j++)
            {
                int n = i * Puffs + j;
                float sc = rad[i] / _bigR;
                float x = _x + _puffVx[n] * sc * b * 1.3f;
                float y = baseY - _puffVy[n] * sc * b + 0.5f * u * 0.12f * b * b;
                _dot.DrawCentered(fb, x, y, fade * (1 - b / 0.7f) * 0.9f);
            }
        }

        float hx = _x, hy = HeadCy, h = _headR;

        // ---- the face ----
        if (t > EyeL) Pick(_eye, t - EyeL).DrawCentered(fb, hx - h * 0.36f, hy - h * 0.18f, fade);
        if (t > EyeR) Pick(_eye, t - EyeR).DrawCentered(fb, hx + h * 0.36f, hy - h * 0.18f, fade);
        if (t > NoseAt) Pick(_nose, t - NoseAt).DrawCentered(fb, hx + h * 0.28f, hy + h * 0.12f, fade);
        for (int i = 0; i < SmileDots; i++)
        {
            float at = SmileAt + i * SmileGap;
            if (t <= at) continue;
            float ang = (30 + 30 * i) * MathF.PI / 180;                  // 30 to 150 degrees round the bottom: ends up, middle down
            Pick(_smile, t - at).DrawCentered(fb, hx + MathF.Cos(ang) * h * 0.52f, hy + h * 0.10f + MathF.Sin(ang) * h * 0.40f, fade);
        }

        // ---- the arms: thick lines from the middle ball's sides ----
        float mx = _x, my = MedCy, mr = _medR;
        float armLen = u * 0.046f, thick = Math.Max(1.5f, u * 0.0028f);
        // Left arm: held out and a little up, still.
        if (t > ArmL) Arm(fb, mx - mr * 0.82f, my - mr * 0.10f, MathF.PI + 0.55f, armLen * Smooth((t - ArmL) / 0.4f), thick, fade);
        // Right arm: rests the same way, then waves.
        if (t > ArmR)
        {
            float e = Smooth((t - WaveFrom) / 0.6f) * Smooth((WaveTo - t) / 0.6f);   // 0 = not waving, 1 = waving
            float ang = 0.55f + e * (0.55f + 0.5f * MathF.Sin(t * 9f));              // radians above level, on the right side
            Arm(fb, mx + mr * 0.82f, my - mr * 0.10f, -ang, armLen * Smooth((t - ArmR) / 0.4f), thick, fade);
        }

        // ---- scarf and hat ----
        if (t > ScarfAt) Pick(_scarf, t - ScarfAt).DrawCentered(fb, hx, hy + h * 0.80f, fade);
        if (t > HatAt) Pick(_hat, t - HatAt).DrawCentered(fb, hx, hy - h * 0.82f, fade);
    }

    /// <summary>A stick arm: a line from the shoulder at an angle (0 = straight right, up is positive via the minus sign below), with two small twig fingers.</summary>
    private void Arm(FrameBuffer fb, float x, float y, float ang, float len, float thick, float fade)
    {
        if (len < 1) return;
        // "ang" is measured with y pointing DOWN on screen, so a negative angle lifts the arm.
        var from = new PointF(x, y);
        var to = new PointF(x + MathF.Cos(ang) * len, y + MathF.Sin(ang) * len);
        fb.Line(from, to, Stick, fade, thick);
        float f = len * 0.28f;
        foreach (float d in new[] { -0.6f, 0.6f })
            fb.Line(to, new PointF(to.X + MathF.Cos(ang + d) * f, to.Y + MathF.Sin(ang + d) * f), Stick, fade, Math.Max(1f, thick * 0.7f));
    }
}
