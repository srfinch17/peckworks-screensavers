using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Peckworks.Screensavers.Core;

namespace SakuraDusk.Happenings;

/// <summary>
/// Basho's haiku, as a tiny play: "the old pond / a frog jumps in / the sound
/// of water." A small round frog sits on the bank by the water. It blinks,
/// puffs its throat once (a croak), crouches, and LEAPS in a long arc out
/// over the pond. It plops in, a little crown of droplets jumps up and falls
/// back, and rings spread over the water. The rings are the last thing left.
///
/// FEYNMAN VERSION: the frog is a flip-book. When it sits we have a handful
/// of pages (eyes open, eyes shut, throat puffed three sizes). For the jump
/// there are 16 pages: a crouch, a launch, and then a body that stretches
/// longer and longer with its legs trailing behind, tipping its nose down as
/// it falls. Every page is painted ONCE in the constructor (a sticker), and
/// each frame we stamp whichever page fits the moment. The frog's path
/// through the air is plain school maths: a parabola, the same arc a thrown
/// ball makes.
///
/// Where it sits is found in Begin by looking at the real pixels: it stands
/// on the bank's walk line (WalkY), a little way up from the water, and the
/// splash spot is checked against the OpenWater stencil so it really lands
/// in the pond. It is drawn in layer 1 (in front of the bank and trees).
/// </summary>
internal sealed class BashoFrog : Happening
{
    private const int JumpPoses = 16;

    private readonly DuskScenery _s;
    private readonly float _u, _size;                   // size unit; the frog's body width in pixels
    private readonly Stamp[] _sit = new Stamp[5];       // eyes open, blink, throat 1/3, 2/3, full
    private readonly Stamp[] _jump = new Stamp[JumpPoses];
    private readonly Stamp _shadow;
    private readonly Stamp[] _dots = new Stamp[3];      // water drops

    // This showing's dice (rolled in Begin).
    private bool _ok;
    private float _dir;                                  // +1 jumps right (from the left bank), -1 left
    private float _gx, _gy;                              // where the frog sits: feet on the ground
    private float _px, _py;                              // where it hits the water
    private float _apex;                                 // how high the arc rises, in pixels
    private float[] _dropAngle = [], _dropSpeed = [];

    private const float Crouch = 4.20f, Launch = 4.60f, Flight = 0.85f;   // the timeline, in seconds
    private float _seconds = 8.5f;

    public override float Seconds => _seconds;
    public override string? Claims => "pond";
    public override int Layer => 1;

    public BashoFrog(DuskScenery s)
    {
        _s = s;
        _u = s.U;
        _size = Math.Max(10f, s.U * 0.028f);     // a small pond frog: well under the tanuki and the lantern, at the same distance
        for (int i = 0; i < 5; i++)
            _sit[i] = MakeFrog(_size, 0f, 0f, blink: i == 1, puff: i < 2 ? 0f : (i - 1) / 3f);
        for (int k = 0; k < JumpPoses; k++)
        {
            Pose(k, out float st, out float tau);
            _jump[k] = MakeFrog(_size, st, AngleAt(tau), false, 0f);
        }
        // Three sizes of water drop: small soft round dots (never under 3 pixels across).
        for (int d = 0; d < 3; d++)
        {
            int dd = Math.Max(3, (int)(s.U * (0.0055f + 0.0020f * d)));
            _dots[d] = Stamp.Paint(dd + 2, dd + 2, g =>
            {
                using var b = new SolidBrush(Color.FromArgb(255, 244, 236));
                g.FillEllipse(b, 1, 1, dd, dd);
            });
        }
        int sw = (int)(_size * 1.5f) + 6, sh = (int)(_size * 0.40f) + 4;
        _shadow = Stamp.Paint(sw, sh, g =>
        {
            using var path = new GraphicsPath();
            path.AddEllipse(1, 1, sw - 2, sh - 2);
            using var br = new PathGradientBrush(path)
            {
                CenterColor = Color.FromArgb(140, 14, 6, 24),
                SurroundColors = [Color.FromArgb(0, 14, 6, 24)],
                Blend = Sprite.SoftFalloff,
            };
            g.FillPath(br, path);
        });
    }

    // ---------------------------------------------------------- the jump

    /// <summary>For jump page k: how stretched the frog is (-0.3 crouched, 1 fully stretched) and how far along the arc (0 to 1).</summary>
    private static void Pose(int k, out float stretch, out float tau)
    {
        if (k == 0) { stretch = -0.30f; tau = 0f; return; }      // the crouch, before the launch
        tau = (k - 1) / (float)(JumpPoses - 2);
        float up = Smooth(tau / 0.22f);                           // stretches out quickly after launch
        float down = 0.25f * Smooth((tau - 0.78f) / 0.22f);       // draws in a little as it nears the water
        stretch = Math.Max(0.15f, up - down);
    }

    /// <summary>How steeply the frog is tilted at this point of the arc. A fixed shape of arc is assumed (rise up, fall down), so the pages can be painted in advance.</summary>
    private static float AngleAt(float tau)
    {
        float slope = -(1 - 2 * tau) * 1.8f + 0.25f;              // positive = nose down
        return Math.Clamp(MathF.Atan(slope), -0.85f, 1.0f);
    }

    // ------------------------------------------------------- the painting

    private static Color Lerp(Color a, Color b, float t) => Color.FromArgb(
        (int)(a.A + (b.A - a.A) * t), (int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));

    /// <summary>
    /// One page: the frog facing right, in "S units" (1 = the sitting body's
    /// width). stretch -0.3..1 runs crouched, sitting, stretched out. theta
    /// tips the whole frog (nose down is positive). The warm rim light on
    /// its front edge is added at the end, from the picture's own pixels.
    /// </summary>
    private static Stamp MakeFrog(float S, float stretch, float theta, bool blink, float puff)
    {
        int size = (int)(3.3f * S) + 4;
        float t = Math.Clamp(stretch, 0f, 1f), c = Math.Clamp(-stretch / 0.3f, 0f, 1f);
        // Evening greens: deeper and duller than daylight, so the frog sits in the
        // dusk like every other creature here, with the warm rim light doing the work.
        Color light = Color.FromArgb(112, 156, 72), mid = Color.FromArgb(78, 124, 58), dark = Color.FromArgb(48, 90, 50);
        Color line = Color.FromArgb(34, 70, 42), belly = Color.FromArgb(206, 186, 138), ink = Color.FromArgb(28, 22, 34);

        return Stamp.Paint(size, size, g =>
        {
            g.TranslateTransform(size / 2f, size / 2f);
            g.ScaleTransform(S, S);
            g.RotateTransform(theta * 180f / MathF.PI);

            PointF P(float sx, float sy, float tx, float ty) => new(sx + (tx - sx) * t, sy + (ty - sy) * t);

            // Legs and arms are thick round-ended lines (hip to knee to foot to toe).
            void Limb(PointF[] pts, float[] widths, Color fill, bool outlineToo = true)
            {
                for (int pass = 0; pass < 2; pass++)
                    for (int i = 0; i + 1 < pts.Length; i++)
                    {
                        float wdt = widths[i] + (pass == 0 ? 0.05f : 0f);
                        using var pen = new Pen(pass == 0 ? line : fill, wdt) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                        g.DrawLine(pen, pts[i], pts[i + 1]);
                    }
            }
            PointF[] HindLeg(float dx, float dy) =>
            [
                P(-0.17f + dx, 0.08f + 0.04f * c + dy, -0.45f + dx, 0.06f + dy),
                P(0.13f + dx, 0.17f - 0.04f * c + dy, -0.92f + dx, 0.10f + dy),
                P(-0.06f + dx, 0.42f + dy, -1.30f + dx, 0.06f + dy),
                P(0.30f + dx, 0.44f + dy, -1.55f + dx, 0.02f + dy),
            ];
            float[] legW = [0.30f - 0.10f * t, 0.15f - 0.04f * t, 0.09f];

            // 1. The far hind leg, a little darker, peeking out behind.
            Limb(HindLeg(0.10f, -0.03f), legW, dark);

            // 1b. The throat puffs out (a croak): a pale bubble behind the chin, so it reads as the throat swelling out of the body.
            if (puff > 0.01f)
            {
                float pr = 0.05f + 0.19f * puff;
                using var sac = new SolidBrush(Color.FromArgb(255, 238, 240, 150));
                using var sacLine = new Pen(line, 0.04f);
                g.FillEllipse(sac, 0.46f - pr * 0.9f, 0.10f, pr * 2.2f, pr * 1.45f);
                g.DrawEllipse(sacLine, 0.46f - pr * 0.9f, 0.10f, pr * 2.2f, pr * 1.45f);
            }

            // 2. The body: a plump rounded shape with a leaf-green gradient.
            float bw = 1f + 0.55f * t + 0.06f * c, bh = 0.80f - 0.26f * t - 0.10f * c;
            float bx = 0.04f * t, by = 0.03f * c;
            RectangleF body = new(bx - bw / 2, by - bh / 2, bw, bh);
            using var bodyPath = new GraphicsPath();
            bodyPath.AddEllipse(body);
            using (var grad = new LinearGradientBrush(new RectangleF(body.X, body.Y - 0.01f, body.Width, body.Height + 0.02f), light, mid, 90f))
                g.FillPath(grad, bodyPath);
            var state = g.Save();
            g.SetClip(bodyPath, CombineMode.Intersect);
            using (var bb = new SolidBrush(belly))                                  // pale yellow belly
                g.FillEllipse(bb, bx - bw * 0.40f, by + bh * 0.10f, bw * 0.84f, bh * 0.60f);
            using (var spot = new SolidBrush(Color.FromArgb(120, dark)))             // three soft spots on the back
            {
                g.FillEllipse(spot, bx - 0.26f * bw, by - bh * 0.32f, 0.12f, 0.09f);
                g.FillEllipse(spot, bx - 0.08f * bw, by - bh * 0.40f, 0.09f, 0.07f);
                g.FillEllipse(spot, bx - 0.36f * bw, by - bh * 0.10f, 0.08f, 0.07f);
            }
            g.Restore(state);
            using (var outline = new Pen(line, 0.045f))
                g.DrawPath(outline, bodyPath);

            // 4. The near hind leg and the arm.
            Limb(HindLeg(0f, 0f), legW, mid);
            Limb([P(0.30f, 0.17f, 0.55f, 0.10f), P(0.40f, 0.43f, 1.00f, 0.06f)], [0.12f - 0.04f * t], mid);
            PointF hand = P(0.41f, 0.44f, 1.02f, 0.06f);
            using (var hb = new SolidBrush(light))
                g.FillEllipse(hb, hand.X - 0.07f, hand.Y - 0.05f, 0.14f, 0.10f);

            // 5. The eyes: two big bulges on top of the head, dark and shiny.
            float er = 0.17f - 0.025f * t;
            PointF e1 = P(0.08f, -0.39f, 0.40f, -0.22f), e2 = P(0.31f, -0.36f, 0.60f, -0.20f);
            foreach (var e in new[] { e1, e2 })
            {
                using var bulge = new SolidBrush(light);
                using var bulgeLine = new Pen(line, 0.04f);
                g.FillEllipse(bulge, e.X - er * 1.1f, e.Y - er * 1.1f, er * 2.2f, er * 2.2f);
                g.DrawEllipse(bulgeLine, e.X - er * 1.1f, e.Y - er * 1.1f, er * 2.2f, er * 2.2f);
                if (blink)
                {
                    using var lid = new Pen(ink, 0.05f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                    g.DrawArc(lid, e.X - er * 0.62f, e.Y - er * 0.30f, er * 1.24f, er * 0.9f, 200, 140);
                }
                else
                {
                    float pr = er * 0.78f;
                    using var pupil = new SolidBrush(ink);
                    g.FillEllipse(pupil, e.X + er * 0.10f - pr, e.Y + er * 0.02f - pr, pr * 2, pr * 2);
                    using var shine = new SolidBrush(Color.White);
                    g.FillEllipse(shine, e.X - er * 0.22f, e.Y - er * 0.40f, er * 0.50f, er * 0.50f);
                    g.FillEllipse(shine, e.X + er * 0.30f, e.Y + er * 0.18f, er * 0.22f, er * 0.22f);
                }
            }

            // 6. A wide little smile, and a hint of blush.
            float mx = 0.12f * t;
            using (var smile = new Pen(line, 0.04f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                g.DrawCurve(smile, new PointF[] { new(0.49f + mx, -0.02f), new(0.43f + mx, 0.08f), new(0.31f + mx, 0.135f), new(0.15f + mx, 0.115f) });
            using (var blush = new SolidBrush(Color.FromArgb(120, 255, 140, 150)))
                g.FillEllipse(blush, 0.20f + mx, 0.03f, 0.15f, 0.09f);
        }, rimSide: 1, rimPx: Math.Max(1, (int)(S * 0.07f)), rim: Color.FromArgb(255, 214, 130), rimAmount: 0.50f);
    }

    // -------------------------------------------------------------- dice

    public override void Begin(Random rng)
    {
        int w = _s.Width, h = _s.Height;
        _ok = false;
        for (int attempt = 0; attempt < 120 && !_ok; attempt++)
        {
            bool left = rng.NextDouble() < 0.5;
            float x = w * (left ? 0.21f + 0.12f * (float)rng.NextDouble() : 0.665f + 0.10f * (float)rng.NextDouble());
            if (_s.Canopies.Any(c => Math.Abs(c.At.X - x) < _u * 0.07f) || Math.Abs(x - w * 0.26f) < _u * 0.05f) continue;   // not in front of a trunk or the lantern
            // Not on the bridge's feet either (0.30 and 0.69 of the width): sitting
            // there it read as a frog climbing onto the bridge, on more than half of scenes.
            float clear = _size * 0.8f + _u * 0.02f;
            if (Math.Abs(x - w * 0.30f) < clear || Math.Abs(x - w * 0.69f) < clear) continue;
            float gy = _s.WalkY(x);
            float dir = left ? 1f : -1f;
            float reach = _u * (0.10f + 0.18f * (float)rng.NextDouble());
            float px = x + dir * reach, py = gy + _u * (-0.05f + 0.07f * (float)rng.NextDouble());
            if (!WaterAround(px, py)) continue;
            _ok = true; _dir = dir; _gx = x; _gy = gy; _px = px; _py = py;
        }
        if (!_ok) { _seconds = 0.3f; return; }
        _seconds = 8.5f;
        _apex = _u * (0.06f + 0.03f * (float)rng.NextDouble()) + 0.22f * Math.Abs(_px - _gx);
        _dropAngle = new float[22]; _dropSpeed = new float[22];
        for (int i = 0; i < 22; i++)
        {
            _dropAngle[i] = ((float)rng.NextDouble() - 0.5f) * 2f * 1.15f;       // up to about 65 degrees from straight up
            _dropSpeed[i] = 0.17f + 0.12f * (float)rng.NextDouble() - 0.05f * MathF.Abs(_dropAngle[i]);
        }
    }

    /// <summary>Is a patch of water around (x, y) wide open (so the splash and rings are not on a bank or the bridge)?</summary>
    private bool WaterAround(float x, float y)
    {
        int w = _s.Width, h = _s.Height;
        float r = _u * 0.04f;
        for (int dx = -1; dx <= 1; dx++)
            for (int dy = 0; dy <= 1; dy++)
            {
                int ix = (int)(x + dx * r), iy = (int)(y + dy * r * 0.5f);
                if (ix < 0 || ix >= w || iy < 0 || iy >= h || !_s.OpenWater[iy * w + ix]) return false;
            }
        return true;
    }

    // -------------------------------------------------------------- draw

    public override void Draw(FrameBuffer fb, float t)
    {
        if (!_ok) return;
        float S = _size;
        float hang = 0.43f * S;                      // from the body's middle down to the ground, when sitting
        float cx0 = _gx, cy0 = _gy - hang;
        float land = Launch + Flight;                // the moment of the plop

        if (t < land)
        {
            float alpha = Smooth(t / 0.7f);
            if (t < Launch) _shadow.Draw(fb, cx0, _gy, 0.5f * alpha, false, null);
            if (t < Crouch)
            {
                // Sitting: blinks twice, then one croak.
                float puff = Smooth((t - 2.9f) / 0.25f) * (1 - Smooth((t - 3.55f) / 0.35f));
                bool blink = (t > 1.4f && t < 1.58f) || (t > 2.05f && t < 2.22f);
                int idx = puff > 0.12f ? (puff < 0.5f ? 2 : puff < 0.85f ? 3 : 4) : blink ? 1 : 0;
                _sit[idx].Draw(fb, cx0, cy0, alpha, _dir < 0, null);
            }
            else if (t < Launch)
            {
                _jump[0].Draw(fb, cx0, cy0 + 0.04f * S, 1f, _dir < 0, null);   // the crouch: gathering itself
            }
            else
            {
                // In the air: a parabola from the bank to the water.
                float tau = (t - Launch) / Flight;
                float x = cx0 + (_px - _dir * 0.10f * S - cx0) * tau;
                float yEnd = _py - 0.15f * S, y = cy0 + (yEnd - cy0) * tau - 4 * _apex * tau * (1 - tau);
                int k = 1 + Math.Min(JumpPoses - 2, (int)(tau * (JumpPoses - 1)));
                float a = tau > 0.90f ? (1 - tau) / 0.10f : 1f;               // slips under the surface
                _jump[k].Draw(fb, MathF.Round(x), MathF.Round(y), a, _dir < 0, null);
            }
        }
        else
        {
            Splash(fb, t - land);
        }
        Rings(fb, t - land);
    }

    /// <summary>A crown of droplets thrown up where the frog went in, falling back under gravity.</summary>
    private void Splash(FrameBuffer fb, float age)
    {
        const float g = 1.15f;                       // gravity, in U per second per second
        for (int i = 0; i < _dropAngle.Length; i++)
        {
            float vx = _dropSpeed[i] * MathF.Sin(_dropAngle[i]) * 0.8f * _u;
            float vy = -(0.10f + _dropSpeed[i] * MathF.Cos(_dropAngle[i]) * 0.9f) * _u;
            float fall = -2 * vy / (g * _u);         // how long until it is back at the water
            if (age < 0 || age > fall) continue;
            float x = _px + vx * age, y = _py + vy * age + 0.5f * g * _u * age * age;
            float a = 0.9f * MathF.Min(1f, (fall - age) / 0.12f);
            _dots[i % 3].Draw(fb, x, y, a, false, null);
        }
        // The dimple: a quick bright ellipse where the water closed over.
        if (age < 0.3f)
            Ring(fb, _px, _py, _u * (0.008f + 0.03f * age), _u * (0.0025f + 0.009f * age), Color.FromArgb(255, 240, 230), 0.7f * (1 - age / 0.3f), Math.Max(1.2f, _u * 0.003f), _s.OpenWater);
    }

    /// <summary>Three thin rings spread over the water, flattened by perspective, each fading over about 2 seconds.</summary>
    private void Rings(FrameBuffer fb, float age0)
    {
        for (int k = 0; k < 3; k++)
        {
            float age = age0 - 0.05f - k * 0.38f;
            const float life = 2.0f;
            if (age <= 0 || age >= life) continue;
            float a = age / life;
            float r = _u * (0.008f + 0.075f * (1 - (1 - a) * (1 - a)));
            float alpha = 0.6f * MathF.Pow(1 - a, 1.3f);
            Ring(fb, _px, _py, r, r * 0.30f, Color.FromArgb(255, 228, 216), alpha, Math.Max(1f, _u * 0.0028f), _s.OpenWater);
        }
    }

    private static void Ring(FrameBuffer fb, float cx, float cy, float rx, float ry, Color c, float alpha, float thick, bool[] only)
    {
        const int seg = 48;
        PointF prev = new(cx + rx, cy);
        for (int i = 1; i <= seg; i++)
        {
            float a = MathF.Tau * i / seg;
            PointF p = new(cx + rx * MathF.Cos(a), cy + ry * MathF.Sin(a));
            float sx = p.X - prev.X, sy = p.Y - prev.Y, sl = MathF.Sqrt(sx * sx + sy * sy);
            float k = sl > 3f ? (sl - 1f) / sl : 1f;                    // stop a pixel short: where two segments meet, the pixel would be painted twice, a bead
            fb.Line(prev, new PointF(prev.X + sx * k, prev.Y + sy * k), c, alpha, thick, only);
            prev = p;
        }
    }

    // ------------------------------------------------ a stamp that can mirror

    /// <summary>
    /// A sprite like Core's, plus two things it cannot do: stamp itself
    /// mirrored (paint the frog facing right once; a frog on the right bank
    /// is the same sticker flipped), and add a warm rim of light to one side
    /// from the finished picture's own pixels. (Core's Sprite is not changed.)
    /// </summary>
    private sealed class Stamp
    {
        private readonly uint[] _p;
        public int W { get; }
        public int H { get; }
        private Stamp(int w, int h, uint[] p) { W = w; H = h; _p = p; }

        public static Stamp Paint(int w, int h, Action<Graphics> paint, int rimSide = 0, int rimPx = 0, Color rim = default, float rimAmount = 0f)
        {
            w = Math.Max(1, w); h = Math.Max(1, h);
            using var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                paint(g);
            }
            var data = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            uint[] px;
            try
            {
                var raw = new int[w * h];
                Marshal.Copy(data.Scan0, raw, 0, raw.Length);
                px = new uint[raw.Length];
                Buffer.BlockCopy(raw, 0, px, 0, raw.Length * 4);
            }
            finally { bmp.UnlockBits(data); }

            if (rimPx > 0 && rimSide != 0)
            {
                // A solid pixel with open air within rimPx pixels on the lit side is on the lit edge: warm it.
                var src = (uint[])px.Clone();
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        uint c = src[y * w + x];
                        if ((c >> 24) < 235) continue;
                        for (int k = 1; k <= rimPx; k++)
                        {
                            int nx = x + rimSide * k;
                            if (nx >= 0 && nx < w && (src[y * w + nx] >> 24) >= 200) continue;
                            float f = rimAmount * (1f - (k - 1f) / rimPx);
                            int r = (int)(((c >> 16) & 0xFF) + (rim.R - ((c >> 16) & 0xFF)) * f);
                            int gg = (int)(((c >> 8) & 0xFF) + (rim.G - ((c >> 8) & 0xFF)) * f);
                            int b = (int)((c & 0xFF) + (rim.B - (c & 0xFF)) * f);
                            px[y * w + x] = (c & 0xFF000000u) | (uint)(r << 16) | (uint)(gg << 8) | (uint)b;
                            break;
                        }
                    }
            }
            return new Stamp(w, h, px);
        }

        /// <summary>Stamps with the CENTER at (cx, cy); mirror = flipped left to right.</summary>
        public void Draw(FrameBuffer fb, float cx, float cy, float opacity, bool mirror, bool[]? only)
        {
            if (opacity <= 0) return;
            int left = (int)(cx - W / 2f), top = (int)(cy - H / 2f);
            int o256 = (int)(Math.Min(1f, opacity) * 256);
            uint[] frame = fb.Pixels;
            for (int sy = Math.Max(0, -top); sy < Math.Min(H, fb.Height - top); sy++)
                for (int dx = Math.Max(0, -left); dx < Math.Min(W, fb.Width - left); dx++)
                {
                    uint c = _p[sy * W + (mirror ? W - 1 - dx : dx)];
                    int a = (int)(c >> 24) * o256 >> 8;
                    if (a == 0) continue;
                    int at = (top + sy) * fb.Width + left + dx;
                    if (only != null && !only[at]) continue;
                    uint bg = frame[at];
                    int br = (int)((bg >> 16) & 0xFF), bgn = (int)((bg >> 8) & 0xFF), bb = (int)(bg & 0xFF);
                    int r = br + (((int)((c >> 16) & 0xFF) - br) * a >> 8);
                    int g = bgn + (((int)((c >> 8) & 0xFF) - bgn) * a >> 8);
                    int b = bb + (((int)(c & 0xFF) - bb) * a >> 8);
                    frame[at] = (uint)((r << 16) | (g << 8) | b);
                }
        }
    }
}
