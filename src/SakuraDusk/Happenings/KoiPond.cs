using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Peckworks.Screensavers.Core;

namespace SakuraDusk.Happenings;

/// <summary>
/// Koi gliding just under the surface of the near pond. Two or three of them
/// (a red-and-white kohaku, a golden yamabuki, a black-spotted orange one)
/// swim slowly across, wriggling gently, seen from above at a shallow angle
/// so they look a little squashed top to bottom. They are dim and softened
/// and tinted violet, because we look at them THROUGH water. They fade in
/// out of the deep and fade out into it. Once, one comes up to the surface to
/// sip, and rings of ripples spread from its nose.
///
/// FEYNMAN VERSION: a fish swims by sending a wave down its body, head to
/// tail, like shaking a rope. To draw that, we paint the fish 16 times (a
/// flip-book), each with the wave in a slightly different place, and flip
/// through the pages while the fish glides along. Each page is a sprite: a
/// sticker painted once and stamped every frame.
///
/// The fish swims through the pond pixels only (the OpenWater stencil), so
/// the banks are always in front of it. The paint-order near the water: the
/// bridge's reflection is already part of the water picture, so the koi pass
/// "under" it for free; the glittering sun path and the falling petals are
/// drawn AFTER this (layer 1), so they stay on top.
/// </summary>
internal sealed class KoiPond : Happening
{
    private const int Poses = 16;                   // pages in the swimming flip-book
    private const int Kinds = 3;                    // kohaku, yamabuki, spotted

    private readonly DuskScenery _s;
    private readonly float _u, _len;                // size unit, a koi's length in pixels
    private readonly Stamp[,] _koi = new Stamp[Kinds, Poses];
    private readonly Stamp _shadow;

    // This showing's dice, rolled in Begin.
    private struct Fish { public int Kind; public float X0, Y0, Dist, Dur, T0, Dir, Ph, Hz; }
    private Fish[] _fish = [];
    private int _sipper;                             // which fish comes up to the surface
    private float _sipT, _sipX, _sipY;               // when and where its ripples start
    private float _seconds = 14f;

    public override float Seconds => _seconds;
    public override string? Claims => "pond";
    public override int Layer => 1;                  // in front of the boat and the lake's glints

    public KoiPond(DuskScenery s)
    {
        _s = s;
        _u = s.U;
        _len = Math.Max(12f, s.U * 0.06f);           // never under 12 px, so the preview box still shows a fish
        for (int k = 0; k < Kinds; k++)
            for (int p = 0; p < Poses; p++)
                _koi[k, p] = MakeKoi(k, p, _len);
        // The shadow on the pond floor: a soft dark ellipse.
        int sw = (int)(_len * 1.0f) + 6, sh = (int)(_len * 0.30f) + 6;
        _shadow = Stamp.Paint(sw, sh, g =>
        {
            using var path = new GraphicsPath();
            path.AddEllipse(2, 2, sw - 4, sh - 4);
            using var br = new PathGradientBrush(path)
            {
                CenterColor = Color.FromArgb(150, 40, 18, 64),
                SurroundColors = [Color.FromArgb(0, 40, 18, 64)],
                Blend = Sprite.SoftFalloff,
            };
            g.FillPath(br, path);
        });
    }

    // ------------------------------------------------------------ the fish

    /// <summary>Looks at the pond through water: pulls a color toward violet.</summary>
    private static Color Water(int r, int g, int b, int a = 255) =>
        Color.FromArgb(a, (int)(r + (96 - r) * 0.18f), (int)(g + (70 - g) * 0.18f), (int)(b + (140 - b) * 0.18f));

    /// <summary>
    /// One page of the flip-book: a koi facing right (head at the right),
    /// with its body wave at "pose" of 16. It is built from a spine (a curved
    /// line down the middle), with the body, fins and patterns hung on it.
    /// </summary>
    private static Stamp MakeKoi(int kind, int pose, float L)
    {
        int pad = (int)(L * 0.08f) + 3;
        int w = (int)MathF.Ceiling(L) + 2 * pad, h = (int)MathF.Ceiling(L * 0.80f) + 2 * pad;
        float phase = MathF.Tau * pose / Poses;

        // A point on the spine, s = 0 at the nose, 1 at the tail tip. The
        // sideways swing is tiny at the head and grows toward the tail.
        PointF Spine(float s)
        {
            float amp = 0.012f + 0.085f * MathF.Pow(s, 1.6f);
            return new PointF((0.5f - s) * L, amp * L * MathF.Sin(MathF.Tau * 0.85f * s - phase));
        }
        // A direction sideways from the spine (the "normal").
        PointF Side(float s)
        {
            PointF a = Spine(Math.Max(0, s - 0.01f)), b = Spine(Math.Min(1, s + 0.01f));
            float dx = b.X - a.X, dy = b.Y - a.Y, len = MathF.Sqrt(dx * dx + dy * dy);
            return new PointF(-dy / len, dx / len);
        }
        PointF Off(float s, float d) { var p = Spine(s); var n = Side(s); return new PointF(p.X + n.X * d, p.Y + n.Y * d); }
        float Half(float s)                           // half the body's width at s
        {
            float p = s < 0.30f ? MathF.Sqrt(Math.Max(0, 1 - MathF.Pow((0.30f - s) / 0.30f, 2)))
                                : 1 - 0.78f * MathF.Pow((s - 0.30f) / 0.44f, 1.3f);
            return 0.140f * L * Math.Max(0.02f, p);
        }

        // Colors for this kind of koi, already tinted by the water.
        Color baseC, finC;
        switch (kind)
        {
            case 0: baseC = Water(240, 232, 234); finC = Water(255, 236, 238, 150); break;   // kohaku: white
            case 1: baseC = Water(240, 186, 60); finC = Water(255, 214, 110, 150); break;    // yamabuki: gold
            default: baseC = Water(232, 124, 58); finC = Water(255, 190, 120, 150); break;   // spotted: orange
        }

        return Stamp.Paint(w, h, g =>
        {
            g.TranslateTransform(w / 2f, h / 2f);
            g.ScaleTransform(1f, 0.74f);              // the shallow viewing angle: everything a little squashed

            // Body outline (down one side, back up the other).
            const int n = 24;
            const float bodyEnd = 0.70f;
            var outline = new List<PointF>();
            for (int i = 0; i <= n; i++) outline.Add(Off(bodyEnd * i / n, Half(bodyEnd * i / n)));
            for (int i = n; i >= 0; i--) outline.Add(Off(bodyEnd * i / n, -Half(bodyEnd * i / n)));

            // 1. A soft halo: the same shape, wider and faint. This is the
            //    "blurred by water" look.
            using (var halo = new Pen(Color.FromArgb(46, baseC), Math.Max(2.5f, L * 0.085f)) { LineJoin = LineJoin.Round })
                g.DrawPolygon(halo, outline.ToArray());

            // 2. The tail fin, a flowing fan, see-through.
            var fin = new List<PointF>
            {
                Off(0.66f, 0.035f * L), Off(0.80f, 0.110f * L), Off(0.92f, 0.200f * L), Off(1.00f, 0.250f * L),
                Off(0.90f, 0.00f), Off(1.00f, -0.250f * L), Off(0.92f, -0.200f * L), Off(0.80f, -0.110f * L), Off(0.66f, -0.035f * L),
            };
            using (var finBrush = new SolidBrush(finC))
                g.FillClosedCurve(finBrush, fin.ToArray(), FillMode.Winding, 0.4f);

            // 3. Pectoral fins: two little paddles near the head, fluttering.
            for (int side = -1; side <= 1; side += 2)
            {
                float flap = 0.80f + 0.20f * MathF.Sin(2 * phase + side);
                PointF root = Off(0.27f, side * Half(0.27f) * 0.85f);
                PointF dir = Side(0.27f);
                PointF tip = new(root.X + dir.X * side * 0.21f * L * flap - 0.10f * L, root.Y + dir.Y * side * 0.21f * L * flap);
                PointF back = new(root.X - 0.15f * L, root.Y);
                PointF mid = new((root.X + tip.X) / 2 + dir.X * side * 0.03f * L, (root.Y + tip.Y) / 2);
                using var fb = new SolidBrush(finC);
                g.FillClosedCurve(fb, new[] { root, mid, tip, back }, FillMode.Winding, 0.5f);
            }

            // 4. The body itself, then its pattern, kept inside the body by a clip.
            using var bodyPath = new GraphicsPath();
            bodyPath.AddPolygon(outline.ToArray());
            using (var bodyBrush = new SolidBrush(baseC))
                g.FillPath(bodyBrush, bodyPath);
            var state = g.Save();
            g.SetClip(bodyPath, CombineMode.Intersect);

            void Blob(float s0, float s1, float side, float rScale, Color c)
            {
                using var br = new SolidBrush(c);
                for (float s = s0; s <= s1; s += 0.012f)
                {
                    PointF p = Off(s, side * Half(s) * 0.35f);
                    float r = Half(s) * rScale;
                    g.FillEllipse(br, p.X - r, p.Y - r, 2 * r, 2 * r);
                }
            }
            if (kind == 0)
            {
                Color red = Water(216, 56, 50);
                Blob(0.04f, 0.18f, 0.0f, 0.85f, red);       // the red cap on the head
                Blob(0.32f, 0.50f, 0.3f, 0.85f, red);
                Blob(0.56f, 0.68f, -0.3f, 0.75f, red);
            }
            else if (kind == 1)
            {
                Blob(0.10f, 0.66f, 0.0f, 0.38f, Water(255, 226, 130, 120));   // a paler sheen down the back
            }
            else
            {
                Color ink = Water(36, 26, 46);
                Blob(0.22f, 0.27f, 0.7f, 0.55f, ink);
                Blob(0.40f, 0.45f, -0.7f, 0.6f, ink);
                Blob(0.56f, 0.60f, 0.5f, 0.5f, ink);
                Blob(0.08f, 0.14f, -0.4f, 0.4f, Water(250, 240, 230));        // one pale patch for contrast
            }
            // A thin bright line down the back: the light on the water.
            using (var sheen = new Pen(Color.FromArgb(46, 255, 255, 255), Math.Max(1f, L * 0.03f)))
                g.DrawLines(sheen, Enumerable.Range(2, 16).Select(i => Off(i / 24f, -Half(i / 24f) * 0.45f)).ToArray());
            g.Restore(state);

            // 5. Two small dark eyes.
            using var eye = new SolidBrush(Color.FromArgb(220, 34, 22, 44));
            float er = Math.Max(1f, L * 0.022f);
            for (int side = -1; side <= 1; side += 2)
            {
                PointF e = Off(0.13f, side * Half(0.13f) * 0.55f);
                g.FillEllipse(eye, e.X - er, e.Y - er, 2 * er, 2 * er);
            }
        });
    }

    // --------------------------------------------------------------- dice

    public override void Begin(Random rng)
    {
        int w = _s.Width, h = _s.Height;
        float top = _s.Bridge.WaterLine + _u * 0.005f, bottom = h - _u * 0.035f;
        int count = rng.Next(2, 4);
        int[] kinds = [0, 1, 2];
        for (int i = 2; i > 0; i--) { int j = rng.Next(i + 1); (kinds[i], kinds[j]) = (kinds[j], kinds[i]); }

        var list = new List<Fish>();
        float schoolDir = rng.NextDouble() < 0.5 ? -1f : 1f;   // a school: they all swim the same way, so they never cross
        for (int i = 0; i < count; i++)
        {
            // Each fish gets its own lane (a band of the near pond).
            float y = top + (bottom - top) * ((i + 0.4f + 0.2f * (float)rng.NextDouble()) / count);
            y = Math.Clamp(y, top, bottom);
            int row = (int)y;
            if (row < 0 || row >= h) continue;
            // How wide is the open water along this lane? Walk out from the middle.
            int cx = (int)(w * (0.49f + 0.04f * (float)rng.NextDouble()));
            if (!_s.OpenWater[row * w + Math.Clamp(cx, 0, w - 1)]) continue;
            int xl = cx, xr = cx;
            while (xl > 0 && _s.OpenWater[row * w + xl - 1]) xl--;
            while (xr < w - 1 && _s.OpenWater[row * w + xr + 1]) xr++;
            float margin = _len * 0.7f;
            float room = xr - xl - 2 * margin;
            if (room < _u * 0.14f) continue;           // too narrow a lane here: skip this fish
            float dist = room * (0.65f + 0.3f * (float)rng.NextDouble());
            float dir = schoolDir;
            float dur = 11.5f;
            float start = dir > 0 ? xl + margin + (room - dist) * (float)rng.NextDouble() : xr - margin - (room - dist) * (float)rng.NextDouble();
            list.Add(new Fish
            {
                Kind = kinds[i], X0 = start, Y0 = y, Dist = dist, Dur = dur, T0 = i * 1.1f, Dir = dir,
                Ph = (float)rng.NextDouble() * MathF.Tau, Hz = 0.75f + 0.2f * (float)rng.NextDouble(),
            });
        }
        list.Sort((a, b) => a.Y0.CompareTo(b.Y0));   // nearer (lower) fish are stamped last
        _fish = list.ToArray();
        if (_fish.Length == 0) { _seconds = 0.3f; return; }
        _seconds = 14f;

        // One of them comes up to sip, about the middle of its swim.
        _sipper = rng.Next(_fish.Length);
        Fish f = _fish[_sipper];
        _sipT = f.T0 + f.Dur * (0.40f + 0.1f * (float)rng.NextDouble());
        float u = (_sipT - f.T0) / f.Dur;
        _sipX = f.X0 + f.Dir * f.Dist * u + f.Dir * _len * 0.50f;     // the nose
        _sipY = f.Y0 + (float)Math.Sin(u * Math.Tau * 1.1 + f.Ph) * Wobble();
    }

    private float Wobble() => Math.Max(1.5f, _u * 0.010f);

    // --------------------------------------------------------------- draw

    public override void Draw(FrameBuffer fb, float t)
    {
        for (int i = 0; i < _fish.Length; i++)
        {
            Fish f = _fish[i];
            float local = t - f.T0;
            if (local <= 0 || local >= f.Dur) continue;
            float u = local / f.Dur;
            float fade = Fade(local, f.Dur, 2.4f, 2.4f);      // up out of the deep, back down into it
            float x = f.X0 + f.Dir * f.Dist * u;
            float y = f.Y0 + (float)Math.Sin(u * Math.Tau * 1.1 + f.Ph) * Wobble();
            int pose = (int)(((double)local * f.Hz + f.Ph / MathF.Tau) * Poses) % Poses;
            if (pose < 0) pose += Poses;

            float op = 0.78f * fade;
            if (i == _sipper)                              // it rises toward the light: clearer for a moment
                op += 0.25f * MathF.Exp(-MathF.Pow((t - _sipT) / 0.8f, 2)) * fade;

            // The shadow lies a little below and behind the fish (on the pond floor).
            _shadow.Draw(fb, x - f.Dir * _len * 0.05f, y + _len * 0.20f, 0.45f * fade, false, _s.OpenWater);
            _koi[f.Kind, pose].Draw(fb, MathF.Round(x), MathF.Round(y), Math.Min(op, 0.95f), f.Dir < 0, _s.OpenWater);
        }

        // The sip: three thin rings spreading from the nose, each a little after the last.
        if (_fish.Length == 0) return;
        for (int k = 0; k < 3; k++)
        {
            float age = t - _sipT - k * 0.45f;
            const float life = 2.6f;
            if (age <= 0 || age >= life) continue;
            float a = age / life;
            float r = _len * 0.12f + _len * 0.85f * (1 - (1 - a) * (1 - a));        // quick at first, then slowing
            float alpha = 0.55f * MathF.Pow(1 - a, 1.4f) * Fade(t, Seconds, 0.1f, 1f);
            Ring(fb, _sipX, _sipY, r, r * 0.30f, Color.FromArgb(255, 226, 214), alpha, Math.Max(1f, _u * 0.0025f), _s.OpenWater);
        }
    }

    /// <summary>A thin ellipse drawn as 48 short straight lines.</summary>
    private static void Ring(FrameBuffer fb, float cx, float cy, float rx, float ry, Color c, float alpha, float thick, bool[] only)
    {
        const int seg = 48;
        PointF prev = new(cx + rx, cy);
        for (int i = 1; i <= seg; i++)
        {
            float a = MathF.Tau * i / seg;
            PointF p = new(cx + rx * MathF.Cos(a), cy + ry * MathF.Sin(a));
            float sx = p.X - prev.X, sy = p.Y - prev.Y, sl = MathF.Sqrt(sx * sx + sy * sy);
            float k = sl > 3f ? (sl - 1f) / sl : 1f;     // stop a pixel short so joints are not painted twice (beads)
            fb.Line(prev, new PointF(prev.X + sx * k, prev.Y + sy * k), c, alpha, thick, only);
            prev = p;
        }
    }

    // ------------------------------------------------ a stamp that can mirror

    /// <summary>
    /// A sprite like Core's, plus one thing it cannot do: stamp itself
    /// mirrored. Paint the fish facing right once, and a fish swimming left
    /// is the same sticker flipped over. (Core's Sprite is not changed.)
    /// </summary>
    private sealed class Stamp
    {
        private readonly uint[] _p;
        public int W { get; }
        public int H { get; }
        private Stamp(int w, int h, uint[] p) { W = w; H = h; _p = p; }

        public static Stamp Paint(int w, int h, Action<Graphics> paint)
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
            try
            {
                var raw = new int[w * h];
                Marshal.Copy(data.Scan0, raw, 0, raw.Length);
                var px = new uint[raw.Length];
                Buffer.BlockCopy(raw, 0, px, 0, raw.Length * 4);
                return new Stamp(w, h, px);
            }
            finally { bmp.UnlockBits(data); }
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
