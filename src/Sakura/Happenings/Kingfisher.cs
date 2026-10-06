using System.Drawing.Drawing2D;
using Peckworks.Screensavers.Core;

namespace Sakura.Happenings;

/// <summary>
/// Kawasemi, the kingfisher: the living jewel of Japan's rivers. One flies in
/// low over the lake, hangs in the air with a blur of wings, folds up and
/// plunges into the water (a crown of droplets and spreading rings), and a
/// moment later bursts out again with a tiny silver fish in its bill and
/// flies away.
///
/// FEYNMAN VERSION: the bird is a flip-book. Every page is the same bird
/// painted with the wings, the tilt of the body and so on a little different
/// (16 pages for a wingbeat, so it flows). A page is painted once, in the
/// constructor, and stamped every frame. Nothing is simulated: Draw is told
/// "t seconds in", and works out which page, and where, from t alone, like
/// reading a timetable.
///
/// The timetable (7 seconds):
///   0.0 to 2.1   fly in from off one side, slowing down as it arrives
///   2.1 to 3.2   hover over its chosen spot, wings a blur
///   3.2 to 3.6   fold up and fall (the body tips nose-down)
///   3.6 to 3.9   slip into the water. The water hides the part already
///                inside: those pages are painted with the lower part cut
///                away, "the waterline" being one row of the page.
///   3.6 on       splash: droplets thrown up on arcs, rings spreading
///   4.7 to 5.0   burst out again, nose up, fish in the bill
///   5.0 to 7.0   fly off the way it was heading
///
/// Where it dives: the painter tells us the lake's pixels (OpenBehindBanks).
/// We try random spots between a tenth and three tenths of a screen height
/// below the horizon and only keep one where the water around it is open
/// (no bank, tree or branch). The bird is stamped only on open pixels, so if
/// its flight ever passes behind a bank it goes behind it.
/// </summary>
internal sealed class Kingfisher : Happening
{
    private const int Beats = 16;                // pages in one wingbeat
    private const int FallPages = 9, DipPages = 12;

    // The timetable, in seconds.
    private const float TIn = 2.1f, TFall = 3.2f, TDive = 3.6f, TDipEnd = 3.9f, TEmerge = 4.7f, TEmergeEnd = 5.0f, TTotal = 7f;

    private readonly Scenery _s;
    private readonly float _u, _l;               // the size unit, and the bird's length in pixels
    private readonly int _side, _wl;             // the page is a square this big; _wl is the waterline row on the dip pages

    // [0] faces right, [1] faces left
    private readonly Sprite[][] _fly = new Sprite[2][];
    private readonly Sprite[][] _hover = new Sprite[2][];
    private readonly Sprite[][] _fall = new Sprite[2][];
    private readonly Sprite[][] _dip = new Sprite[2][];
    private readonly Sprite[][] _rise = new Sprite[2][];
    private readonly Sprite[][] _flyFish = new Sprite[2][];

    // This showing's dice.
    private bool _ok;
    private int _dir = 1;
    private float _xd, _yd;                      // the dive spot (where it meets the water)
    private readonly float[] _dvx = new float[16], _dvy = new float[16], _dsz = new float[16];

    public override float Seconds => _ok ? TTotal : 0.3f;
    public override int Layer => 1;                         // in front of the boat and the lake's glints
    public override string? Claims => "water";

    private static readonly Color Back = Color.FromArgb(22, 142, 200);
    private static readonly Color BackLight = Color.FromArgb(84, 226, 244);
    private static readonly Color Belly = Color.FromArgb(238, 134, 54);
    private static readonly Color Outline = Color.FromArgb(10, 44, 84);

    public Kingfisher(Scenery s)
    {
        _s = s;
        _u = s.U;
        _l = Math.Max(14f, s.U * 0.036f);
        _side = (int)MathF.Ceiling(1.7f * _l);
        _wl = (int)MathF.Ceiling(1.3f * _l);

        for (int d = 0; d < 2; d++)
        {
            bool left = d == 1;
            _fly[d] = new Sprite[Beats];
            _flyFish[d] = new Sprite[Beats];
            _hover[d] = new Sprite[Beats];
            for (int i = 0; i < Beats; i++)
            {
                float ph = MathF.Tau * i / Beats;
                float wing = -0.1f + 1.05f * MathF.Sin(ph);                   // radians: negative is up
                _fly[d][i] = Page(left, -6f, wing, 0f, 0f, false, -1);
                _flyFish[d][i] = Page(left, -20f, wing, 0f, 0f, true, -1);
                // Hovering: body held nose-up, the wings beat so fast they are a smear.
                _hover[d][i] = Page(left, -24f, -0.15f + 0.7f * MathF.Sin(ph), 1f, 0f, false, -1, 0.04f * MathF.Sin(ph * 2));
            }
            _fall[d] = new Sprite[FallPages];
            for (int i = 0; i < FallPages; i++)
            {
                float k = i / (float)(FallPages - 1);
                _fall[d][i] = Page(left, 20f + 62f * k, 0f, 0f, Smooth(k * 1.6f), false, -1);   // wings fold back as it tips over
            }
            _dip[d] = new Sprite[DipPages];
            _rise[d] = new Sprite[DipPages];
            for (int i = 0; i < DipPages; i++)
            {
                float k = i / (float)(DipPages - 1);
                // Dive: the nose goes in first. The centre travels from just above the water to well below it.
                _dip[d][i] = Page(left, 80f, 0f, 0f, 1f, false, _wl, -0.6f + 1.3f * k);
                // Burst out: the mirror of that, nose up, wings opening, fish in the bill.
                _rise[d][i] = Page(left, -62f, -0.5f + 0.5f * k, 0f, 1f - k, true, _wl, 0.42f - 1.2f * k);
            }
        }
    }

    // ---------------------------------------------------------------- dice

    public override void Begin(Random rng)
    {
        _ok = false;
        _dir = rng.Next(2) == 0 ? 1 : -1;
        bool[] open = _s.OpenBehindBanks;
        int w = _s.Width, h = _s.Height;
        float yLo = _s.HorizonY + _u * 0.10f;
        float yHi = Math.Min(_s.HorizonY + _u * 0.30f, h - _u * 0.05f);
        if (yHi < yLo) yHi = yLo;
        float reach = _u * 0.10f;
        for (int tries = 0; tries < 80 && !_ok; tries++)
        {
            float x = w * (0.22f + 0.62f * (float)rng.NextDouble());
            float y = yLo + (yHi - yLo) * (float)rng.NextDouble();
            // Open water all round the dive (rings spread there), and under the hover.
            bool clear = true;
            for (int k = 0; k < 8 && clear; k++)
            {
                float a = k * MathF.Tau / 8;
                clear = IsOpen(open, x + reach * MathF.Cos(a), y + reach * 0.3f * MathF.Sin(a));
            }
            clear = clear && IsOpen(open, x, y) && IsOpen(open, x - _dir * _u * 0.05f, y - _u * 0.11f)
                          && IsOpen(open, x - _dir * _u * 0.05f, y - _u * 0.05f);
            if (clear) { _xd = x; _yd = y; _ok = true; }
        }
        for (int i = 0; i < _dvx.Length; i++)
        {
            float a = (i + 0.5f) / _dvx.Length * 2 - 1 + (float)(rng.NextDouble() - 0.5) * 0.1f;   // -1 to 1, evenly spread so no two droplets clump
            _dvx[i] = a * 0.13f + (float)(rng.NextDouble() - 0.5) * 0.03f;       // sideways speed, U per second
            _dvy[i] = 0.26f + 0.17f * (1 - MathF.Abs(a)) * (float)rng.NextDouble() + 0.06f * (float)rng.NextDouble();   // upward speed
            _dsz[i] = 0.7f + 0.8f * (float)rng.NextDouble();
        }
    }

    private bool IsOpen(bool[] open, float x, float y)
    {
        int xi = (int)x, yi = (int)y;
        if (xi < 0 || xi >= _s.Width || yi < 0 || yi >= _s.Height) return false;
        return open[yi * _s.Width + xi];
    }

    // ---------------------------------------------------------------- draw

    public override void Draw(FrameBuffer fb, float t)
    {
        if (!_ok) return;
        bool[] st = _s.OpenBehindBanks;
        int d = _dir > 0 ? 0 : 1;
        float dir = _dir;
        float hoverX = _xd - dir * _u * 0.05f;
        float hoverY = _yd - _u * 0.11f;
        float wlY = _yd;                                         // the water surface where it dives

        // The water effects go first, so the bird is drawn over its own splash.
        Splash(fb, t, st);

        // ---- 1. fly in, hover ----
        if (t < TFall + 0.1f)
        {
            float q = Math.Clamp(t / TIn, 0f, 1f);
            float ease = 1 - (1 - q) * (1 - q);                              // fast at first, settling
            float x0 = dir > 0 ? -_l : _s.Width + _l;
            float x = x0 + (hoverX - x0) * ease;
            float y = hoverY + _u * 0.04f * (1 - ease) + Math.Max(1.5f, _u * 0.004f) * MathF.Sin(t * 5.2f) * (t > 1.5f ? 1 : 0.5f);
            if (t >= TIn) x += Math.Max(1.5f, _u * 0.0035f) * MathF.Sin(t * 3.1f);
            float hf = Smooth((t - 1.55f) / 0.4f);                           // flight pages melt into hover pages
            float fade = Fade(t, 99f, 0.2f, 1f);                             // a soft arrival at the very edge
            int beatF = (int)(t * 4.2f * Beats) % Beats;
            int beatH = (int)(t * 7f * Beats) % Beats;
            float over = t < TFall ? 1f : 1 - (t - TFall) / 0.1f;            // the hover pages melt away as the fall starts
            Stamp(fb, _fly[d][beatF], x, y, (1 - hf) * fade, st);
            Stamp(fb, _hover[d][beatH], x, y, hf * over * fade, st);
        }

        // ---- 2. the fall ----
        if (t >= TFall && t < TDive)
        {
            float q = (t - TFall) / (TDive - TFall);
            float yEnd = wlY - 0.6f * _l;
            float x = hoverX + dir * _u * 0.05f * Smooth(q);
            float y = hoverY + (yEnd - hoverY) * q * q;
            Stamp(fb, _fall[d][Math.Min(FallPages - 1, (int)(q * FallPages))], x, y, Math.Min(1f, (t - TFall) / 0.1f), st);
        }

        // ---- 3. slipping in ----
        if (t >= TDive && t < TDipEnd)
        {
            float k = (t - TDive) / (TDipEnd - TDive);
            Sprite pg = _dip[d][Math.Min(DipPages - 1, (int)(k * DipPages))];
            pg.Draw(fb, (int)MathF.Round(_xd - _side / 2f), (int)MathF.Round(wlY - _wl), 1f, st);
        }

        // ---- 4. bursting out, then flying off ----
        if (t >= TEmerge)
        {
            float xe = _xd + dir * _u * 0.015f;
            if (t < TEmergeEnd + 0.12f)
            {
                float k = Math.Clamp((t - TEmerge) / (TEmergeEnd - TEmerge), 0f, 1f);
                Sprite pg = _rise[d][Math.Min(DipPages - 1, (int)(k * DipPages))];
                float over = t < TEmergeEnd ? 1f : 1 - (t - TEmergeEnd) / 0.12f;
                pg.Draw(fb, (int)MathF.Round(xe - _side / 2f), (int)MathF.Round(wlY - _wl), over, st);
            }
            if (t >= TEmergeEnd)
            {
                float q = (t - TEmergeEnd) / (TTotal - TEmergeEnd);
                float xEnd = dir > 0 ? _s.Width + _l : -_l;
                float x = xe + (xEnd - xe) * (q * q * (0.6f + 0.4f * q) + 0.02f * q);        // speeds up as it goes
                float rise = Smooth(Math.Min(1f, (t - TEmergeEnd) / 0.7f));
                float y = wlY - 0.78f * _l - rise * _u * 0.10f - Math.Min(1f, q * 3) * _u * 0.03f;
                int beat = (int)(t * 4.6f * Beats) % Beats;
                Stamp(fb, _flyFish[d][beat], x, y, Math.Min(1f, (t - TEmergeEnd) / 0.12f), st);
            }
        }
    }

    private void Stamp(FrameBuffer fb, Sprite p, float x, float y, float opacity, bool[] st) =>
        p.Draw(fb, (int)MathF.Round(x - _side / 2f), (int)MathF.Round(y - _side / 2f), opacity, st);

    // ---------------------------------------------------------------- the water

    /// <summary>
    /// Everything the water does: the crown of droplets, the spreading rings,
    /// and a smaller echo when the bird comes out. Each droplet is thrown up
    /// with its own speed and then simply falls (height = v*t - g*t*t/2),
    /// the same as a ball you toss in the air.
    /// </summary>
    private void Splash(FrameBuffer fb, float t, bool[] st)
    {
        float u = _u;
        float ringW = Math.Max(1f, u * 0.0032f);
        // Rings: the dive makes three, the exit one.
        Ring(fb, t - TDive, 0f, 1f, ringW, st);
        Ring(fb, t - TDive, 0.28f, 0.8f, ringW, st);
        Ring(fb, t - TDive, 0.56f, 0.6f, ringW, st);
        Ring(fb, t - TEmerge, 0f, 0.9f, ringW, st);
        Ring(fb, t - TEmerge, 0.22f, 0.55f, ringW, st);

        Droplets(fb, t - TDive, 16, 1f, st);
        Droplets(fb, t - TEmerge, 9, 0.75f, st);

        // The first instant of the dive: a few thin jets of water thrown up and out,
        // each a little arc that thins as it climbs, and a flash of foam on the surface.
        float c = t - TDive;
        if (c >= 0 && c < 0.5f)
        {
            float k = c / 0.5f;
            float a = (1 - k) * (1 - k);
            for (int i = -3; i <= 3; i++)
            {
                float spread = i * 0.008f * u * (0.5f + 1.2f * k);
                float top = u * (0.036f - 0.005f * MathF.Abs(i)) * MathF.Sin(MathF.Min(1f, k * 1.5f) * MathF.PI * 0.5f);
                PointF prev = new(_xd, _yd);
                for (int sgm = 1; sgm <= 6; sgm++)
                {
                    float f = sgm / 6f;
                    var p = new PointF(_xd + spread * f * (1 + 0.4f * f), _yd - top * (1 - (1 - f) * (1 - f) * 0.5f) * f * 1.1f);
                    fb.Line(prev, p, Color.FromArgb(238, 248, 255), 0.8f * a * (1 - f * 0.55f), Math.Max(1f, u * 0.003f * (1 - f * 0.6f)), st);
                    prev = p;
                }
            }
            Ring(fb, c, 0f, 1.2f, Math.Max(2f, u * 0.006f), st, 0.35f);      // a short thick foam ring on the surface
        }
    }

    /// <summary>
    /// A ring on the water: a flattened circle (we look at the lake from a slant) that grows and fades.
    /// Worked out per pixel: how far is this pixel from the ellipse? The pixel's own distance is
    /// (d - 1) divided by how fast d changes, where d is 1 exactly on the ring.
    /// </summary>
    private void Ring(FrameBuffer fb, float age, float delay, float strength, float width, bool[] st, float life = 1.9f)
    {
        age -= delay;
        if (age <= 0 || age > life) return;
        float q = age / life;
        float reach = life < 1f ? 0.05f : 0.115f;
        float rx = _u * (0.012f + reach * MathF.Pow(q, 0.62f));
        float ry = rx * 0.27f;
        float a = strength * 0.6f * MathF.Pow(1 - q, 1.4f) * Smooth(age / 0.08f);
        if (a < 0.02f) return;
        int x0 = Math.Max(0, (int)(_xd - rx - width - 2)), x1 = Math.Min(fb.Width - 1, (int)(_xd + rx + width + 2));
        int y0 = Math.Max(0, (int)(_yd - ry - width - 2)), y1 = Math.Min(fb.Height - 1, (int)(_yd + ry + width + 2));
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                float dx = x + 0.5f - _xd, dy = y + 0.5f - _yd;
                float ex = dx / rx, ey = dy / ry;
                float d = MathF.Sqrt(ex * ex + ey * ey);
                if (d < 0.01f) continue;
                float gx = ex / (rx * d), gy = ey / (ry * d);
                float dist = (d - 1) / MathF.Sqrt(gx * gx + gy * gy);
                float cover = Math.Clamp(width / 2 + 0.5f - MathF.Abs(dist), 0f, 1f);
                if (cover <= 0) continue;
                int at = y * fb.Width + x;
                if (!st[at]) continue;
                float side = 0.6f + 0.4f * Math.Clamp(dy / ry, -1f, 1f);          // the far side of a ring looks fainter
                fb.Pixels[at] = FrameBuffer.Blend(fb.Pixels[at], 238, 247, 255, a * cover * side);
            }
    }
    private void Droplets(FrameBuffer fb, float age, int count, float scale, bool[] st)
    {
        if (age < 0 || age > 1.1f) return;
        const float g = 1.35f;                                   // gravity in U per second squared
        for (int i = 0; i < count; i++)
        {
            float vy = _dvy[i] * scale, vx = _dvx[i] * scale;
            float up = vy * age - g * age * age / 2;             // height above the water
            if (up < 0) continue;                                // it has landed: gone
            float x = _xd + _u * vx * age * 1.4f;
            float y = _yd - _u * up;
            float r = Math.Max(1.0f, _u * 0.0018f * _dsz[i] * scale);
            float a = Math.Min(1f, (1.1f - age) * 3f);
            Dot(fb, x, y, r, Color.FromArgb(240, 249, 255), 0.85f * a, st);
            
        }
    }

    /// <summary>A soft round dot, drawn straight into the picture. The edge pixels are part covered, so it looks round at any size.</summary>
    private static void Dot(FrameBuffer fb, float cx, float cy, float r, Color c, float alpha, bool[] st)
    {
        int x0 = (int)MathF.Floor(cx - r - 1), x1 = (int)MathF.Ceiling(cx + r + 1);
        int y0 = (int)MathF.Floor(cy - r - 1), y1 = (int)MathF.Ceiling(cy + r + 1);
        for (int y = Math.Max(0, y0); y <= Math.Min(fb.Height - 1, y1); y++)
            for (int x = Math.Max(0, x0); x <= Math.Min(fb.Width - 1, x1); x++)
            {
                float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                float cover = Math.Clamp(r + 0.5f - MathF.Sqrt(dx * dx + dy * dy), 0f, 1f);
                if (cover <= 0) continue;
                int at = y * fb.Width + x;
                if (!st[at]) continue;
                fb.Pixels[at] = FrameBuffer.Blend(fb.Pixels[at], c.R, c.G, c.B, alpha * cover);
            }
    }

    // ---------------------------------------------------------------- painting

    /// <summary>
    /// One page. pitch: degrees the body is tipped nose-down (negative = nose up).
    /// wing: the near wing's angle in radians (negative is up). fan: 0 = one crisp
    /// wing, 1 = a smear of wings (hovering). tuck: 0 = wings out, 1 = folded back.
    /// waterRow: when 0 or more, everything below that row is cut off (the part
    /// that is under water); then "along" is where the bird's centre is, in
    /// bird lengths below the waterline.
    /// </summary>
    private Sprite Page(bool left, float pitch, float wing, float fan, float tuck, bool fish, int waterRow, float along = 0f)
    {
        float cy = waterRow >= 0 ? waterRow + along * _l : _side / 2f;
        return Sprite.Paint(_side, _side, g =>
        {
            if (waterRow >= 0) g.SetClip(new RectangleF(0, 0, _side, waterRow));
            g.TranslateTransform(_side / 2f, cy);
            g.ScaleTransform(left ? -_l : _l, _l);       // from here on we paint in "bird lengths": the bird is 1 long
            g.RotateTransform(pitch);
            Bird(g, wing, fan, tuck, fish);
        });
    }

    private static PointF P(float x, float y) => new(x, y);

    /// <summary>The bird itself, facing right, centred on its body, 1 unit from tail to bill tip.</summary>
    private static void Bird(Graphics g, float wing, float fan, float tuck, bool fish)
    {
        using var edge = new Pen(Color.FromArgb(200, Outline), 0.03f) { LineJoin = LineJoin.Round };

        // far wing: dimmer, a touch behind the near one
        if (fan < 0.5f) Wing(g, wing * 0.7f - 0.2f, tuck, 0.55f, true);

        // tail: a short blue wedge
        PointF[] tail = [P(-0.18f, -0.04f), P(-0.40f, -0.06f), P(-0.41f, 0.045f), P(-0.18f, 0.08f)];
        using (var b = new SolidBrush(Color.FromArgb(24, 116, 180))) g.FillPolygon(b, tail);
        g.DrawPolygon(edge, tail);

        // little red feet, tucked under
        using (var foot = new Pen(Color.FromArgb(222, 70, 52), 0.03f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
        {
            g.DrawLine(foot, P(0.02f, 0.12f), P(0.0f, 0.2f));
            g.DrawLine(foot, P(0.08f, 0.12f), P(0.09f, 0.19f));
        }

        // body: blue all over, then an orange belly clipped to the body's shape
        using var body = new GraphicsPath();
        body.AddEllipse(-0.27f, -0.14f, 0.54f, 0.31f);
        using (var b = new SolidBrush(Back)) g.FillPath(b, body);
        var save = g.Save();
        g.SetClip(body, CombineMode.Intersect);
        using (var b = new SolidBrush(Belly)) g.FillEllipse(b, -0.30f, 0.0f, 0.66f, 0.34f);
        using (var b = new SolidBrush(Color.FromArgb(120, 255, 190, 110))) g.FillEllipse(b, -0.05f, 0.06f, 0.34f, 0.12f);   // a lighter glow on the orange
        // the bright cyan stripe down the back
        using (var stripe = new Pen(BackLight, 0.055f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            g.DrawLine(stripe, P(-0.22f, -0.075f), P(0.14f, -0.105f));
        g.Restore(save);
        g.DrawPath(edge, body);

        // head: big for the bird's size (that is what makes it cute, and it is accurate)
        using var head = new GraphicsPath();
        head.AddEllipse(0.06f, -0.20f, 0.33f, 0.31f);
        using (var b = new SolidBrush(Color.FromArgb(28, 140, 198))) g.FillPath(b, head);
        save = g.Save();
        g.SetClip(head, CombineMode.Intersect);
        using (var b = new SolidBrush(Color.FromArgb(24, 100, 170))) g.FillEllipse(b, 0.04f, -0.25f, 0.4f, 0.17f);              // darker crown
        using (var b = new SolidBrush(Color.FromArgb(240, 138, 62))) g.FillEllipse(b, 0.10f, -0.07f, 0.17f, 0.16f);            // orange cheek
        using (var b = new SolidBrush(Color.FromArgb(252, 250, 244))) g.FillEllipse(b, 0.20f, 0.02f, 0.14f, 0.12f);           // white throat
        using (var b = new SolidBrush(Color.FromArgb(250, 248, 240))) g.FillEllipse(b, 0.065f, -0.09f, 0.055f, 0.07f);         // the pale spot on the neck
        g.Restore(save);
        g.DrawPath(edge, head);

        // dagger bill
        PointF[] bill = [P(0.33f, -0.075f), P(0.64f, 0.005f), P(0.33f, 0.04f)];
        using (var b = new SolidBrush(Color.FromArgb(34, 36, 44))) g.FillPolygon(b, bill);
        using (var b = new SolidBrush(Color.FromArgb(150, 90, 60, 50))) g.FillPolygon(b, [P(0.33f, 0.0f), P(0.5f, 0.012f), P(0.33f, 0.04f)]);

        // the eye: big, round, dark, with a bright highlight
        using (var b = new SolidBrush(Color.FromArgb(18, 16, 24))) g.FillEllipse(b, 0.255f, -0.12f, 0.075f, 0.075f);
        using (var b = new SolidBrush(Color.White)) g.FillEllipse(b, 0.29f, -0.112f, 0.027f, 0.027f);

        // near wing, on top of the body
        if (fan >= 0.5f) WingFan(g, wing, tuck);
        else Wing(g, wing, tuck, 1f, false);

        if (fish) Fish(g);
    }

    /// <summary>A blurred wing: several ghost wings across the arc of one beat, one stronger in the middle.</summary>
    private static void WingFan(Graphics g, float wing, float tuck)
    {
        for (int i = 0; i < 7; i++)
        {
            float a = -1.05f + 1.75f * i / 6f;
            Wing(g, a, tuck, 0.2f, false);
        }
        Wing(g, wing, tuck, 0.75f, false);
    }

    /// <summary>One wing as a leaf shape from the shoulder. angle: radians, negative = up; tuck: 0 = out, 1 = folded back along the body.</summary>
    private static void Wing(Graphics g, float angle, float tuck, float alpha, bool far)
    {
        PointF sh = P(0.04f, -0.075f);
        float dx = -0.30f - 0.12f * MathF.Abs(MathF.Sin(angle)), dy = MathF.Sin(angle);
        dx = dx + (-1f - dx) * tuck;
        dy = dy + (0.03f - dy) * tuck;
        float n = MathF.Sqrt(dx * dx + dy * dy);
        dx /= n; dy /= n;
        float len = 0.52f - 0.12f * tuck;
        PointF tip = P(sh.X + dx * len, sh.Y + dy * len);
        float px = -dy, py = dx;                                  // sideways
        if (px * 1f + py * -0.6f < 0) { px = -px; py = -py; }     // point the "front edge" side toward the bird's front/top
        PointF rf = P(sh.X + px * 0.075f, sh.Y + py * 0.075f), rb = P(sh.X - px * 0.085f, sh.Y - py * 0.085f);
        PointF mf = P(sh.X + dx * len * 0.55f + px * 0.07f, sh.Y + dy * len * 0.55f + py * 0.07f);
        PointF mb = P(sh.X + dx * len * 0.6f - px * 0.10f, sh.Y + dy * len * 0.6f - py * 0.10f);
        PointF[] shape = [rf, mf, tip, mb, rb];
        int a = (int)(255 * alpha);
        using (var b = new SolidBrush(far ? Color.FromArgb(a, 18, 92, 156) : Color.FromArgb(a, 30, 124, 196)))
            g.FillClosedCurve(b, shape, FillMode.Winding, 0.5f);
        if (alpha > 0.5f)
        {
            // lighter covert band near the shoulder, and a dark edge so it reads against the lake
            PointF m1 = P(sh.X + dx * len * 0.12f, sh.Y + dy * len * 0.12f);
            PointF[] cov = [P(rf.X * 0.7f + m1.X * 0.3f, rf.Y * 0.7f + m1.Y * 0.3f), P(mf.X * 0.55f + sh.X * 0.45f, mf.Y * 0.55f + sh.Y * 0.45f),
                            P(sh.X + dx * len * 0.62f, sh.Y + dy * len * 0.62f), P(mb.X * 0.55f + sh.X * 0.45f, mb.Y * 0.55f + sh.Y * 0.45f),
                            P(rb.X * 0.7f + m1.X * 0.3f, rb.Y * 0.7f + m1.Y * 0.3f)];
            using (var b = new SolidBrush(Color.FromArgb((int)(a * 0.75f), 70, 214, 240)))
                g.FillClosedCurve(b, cov, FillMode.Winding, 0.5f);
            using var pen = new Pen(Color.FromArgb((int)(a * 0.8f), Outline), 0.025f) { LineJoin = LineJoin.Round };
            g.DrawClosedCurve(pen, shape, 0.5f, FillMode.Winding);
        }
    }

    /// <summary>A small silver fish held crosswise in the bill.</summary>
    private static void Fish(Graphics g)
    {
        var save = g.Save();
        g.TranslateTransform(0.56f, 0.06f);
        g.RotateTransform(76f);
        using var body = new GraphicsPath();
        body.AddEllipse(-0.17f, -0.05f, 0.34f, 0.10f);
        using (var b = new SolidBrush(Color.FromArgb(226, 236, 244))) g.FillPath(b, body);
        using (var b = new SolidBrush(Color.FromArgb(120, 150, 172))) g.FillEllipse(b, -0.15f, -0.05f, 0.30f, 0.04f);
        using (var b = new SolidBrush(Color.FromArgb(210, 226, 238))) g.FillPolygon(b, [P(-0.14f, 0f), P(-0.26f, -0.07f), P(-0.23f, 0f), P(-0.26f, 0.07f)]);
        using (var b = new SolidBrush(Color.FromArgb(20, 20, 28))) g.FillEllipse(b, 0.10f, -0.022f, 0.032f, 0.032f);
        using (var pen = new Pen(Color.FromArgb(150, 50, 80, 110), 0.012f)) g.DrawPath(pen, body);
        g.Restore(save);
    }
}
