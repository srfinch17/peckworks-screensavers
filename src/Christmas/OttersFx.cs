using Peckworks.Screensavers.Core;
using Peckworks.Screensavers.Core.Minds;

namespace Christmas;

// The otters' small effects: ripples in the hole, droplets and splashes, puffs of snow, the line of bubbles under
// the ice, the pale scratch a belly slide leaves. Every one is a cheap little thing kept in a fixed array (no
// allocation per frame), fading out by itself. None of them steers anything: they only sell the world.
internal sealed partial class Otters
{
    /// <summary>A ripple ring: grows from nothing to "reach" metres across 1.5 s, fading. Rings in the hole never grow past its edge.</summary>
    private void AddRing(float x, float y, float reach)
    {
        if (_ringN >= _rings.Length) { _rings[0] = _rings[--_ringN]; }
        _rings[_ringN++] = new Ring { X = x, Y = y, Age = 0f, Max = 1.5f, MaxR = reach };
    }

    // ---- THE HOLE IN TWO PIECES (2026-11-10) ----
    // A hole has to be a place that exists. The painter's backdrop already holds the dark water and the pale rim; here are the
    // two pieces an animal can pass between. The BACK: a thin lighter crescent along the far inside wall (the cut thickness of
    // the ice seen through the water), drawn before any otter. The FRONT LIP: the near rim of the ice and a sliver of the sheet in
    // front of it, cut from the backdrop's OWN pixels so it matches exactly, drawn AFTER the otter, so whatever of the animal
    // is down at the rim is really hidden by something in front of it (a trapdoor on a stage works the same way).
    private Sprite? _lip, _back; private int _lipX, _lipY, _backX, _backY;
    private float _lipA = 1f, _nearHold;                 // how much of the front lip is drawn (it covers the part of a near-rim haul still in the water, then eases away)
    private float _wob, _wobPh, _wobShown;                              // how much the water of the hole is rocking (1 just after a splash, easing to 0), and its phase

    private static float Soft(float a, float b, float v) { float t = Math.Clamp((v - a) / (b - a), 0f, 1f); return t * t * (3f - 2f * t); }

    private void BuildHole()
    {
        uint[] px = S.Pixels; int W = S.Width, H = S.Height;
        int x0 = Math.Max(0, (int)(_hx - _hrx * 1.6f)), x1 = Math.Min(W - 1, (int)(_hx + _hrx * 1.6f));
        int y0 = Math.Max(0, (int)(_hy - _hry * 1.4f)), y1 = Math.Min(H - 1, (int)(_hy + _hry * 1.8f));
        int w = x1 - x0 + 1, h = y1 - y0 + 1;
        if (w < 4 || h < 4) return;
        var lip = new uint[w * h]; var back = new uint[w * h];
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                float dx = (x + 0.5f - _hx) / _hrx, dy = (y + 0.5f - _hy) / _hry, rr = MathF.Sqrt(dx * dx + dy * dy);
                if (rr < 1e-3f) continue;
                uint c = px[y * W + x]; int i = (y - y0) * w + (x - x0);
                // front lip: the ring from just inside the rim to a sliver of sheet beyond it, strongest on the near (lower) arc
                float band = Soft(0.93f, 1.0f, rr) * (1f - Soft(1.28f, 1.55f, rr));
                float near = Soft(0.22f, 0.62f, dy / rr);
                float a = band * near;
                if (a > 0.01f)
                {
                    float sh = 1f - 0.16f * (1f - Soft(1.0f, 1.12f, rr));        // the inside of the lip is in its own shadow
                    int r = (int)(((c >> 16) & 255) * sh), g = (int)(((c >> 8) & 255) * sh), b = (int)((c & 255) * sh);
                    lip[i] = ((uint)(a * 255) << 24) | ((uint)r << 16) | ((uint)g << 8) | (uint)b;
                }
                // back crescent: the far inner wall, a lighter teal-blue line fading toward the middle of the water
                float wall = Soft(0.78f, 0.98f, rr) * (1f - Soft(0.98f, 1.02f, rr)) * Soft(0.15f, 0.6f, -dy / rr);
                if (wall > 0.01f) back[i] = ((uint)(wall * 120) << 24) | (60u << 16) | (104u << 8) | 150u;
            }
        _lip = Sprite.FromPixels(w, h, lip); _back = Sprite.FromPixels(w, h, back);
        _lipX = _backX = x0; _lipY = _backY = y0;
    }

    /// <summary>
    /// A soft continuous ring on the frame: every pixel near the ellipse is lightened by how close it lies to the ellipse's line (the
    /// distance is measured across the line, not along the axes, so the stroke has the same width all the way round), strongest on the
    /// near side. "clip": only inside the hole's water. Written pixel by pixel so there are no joins to make dots.
    /// </summary>
    private void SoftRing(FrameBuffer fb, float cx, float cy, float rx, float ry, float alpha, float thick, bool clip)
    {
        if (rx < 1.5f || ry < 0.6f || alpha < 0.01f) return;
        ry = MathF.Max(ry, 0.8f);
        int x0 = Math.Max(0, (int)MathF.Floor(cx - rx - thick - 2)), x1 = Math.Min(fb.Width - 1, (int)MathF.Ceiling(cx + rx + thick + 2));
        int y0 = Math.Max(0, (int)MathF.Floor(cy - ry - thick - 2)), y1 = Math.Min(fb.Height - 1, (int)MathF.Ceiling(cy + ry + thick + 2));
        float half = thick * 0.5f + 0.7f;
        for (int y = y0; y <= y1; y++)
        {
            float dy = y + 0.5f - cy;
            for (int x = x0; x <= x1; x++)
            {
                float dx = x + 0.5f - cx;
                float q = MathF.Sqrt(dx * dx / (rx * rx) + dy * dy / (ry * ry));
                if (q < 1e-3f) continue;
                float gx = dx / (rx * rx * q), gy = dy / (ry * ry * q), gl = MathF.Sqrt(gx * gx + gy * gy);
                float dist = MathF.Abs(q - 1f) / MathF.Max(gl, 1e-4f);
                if (dist >= half) continue;
                if (clip && !InHole(x + 0.5f, y + 0.5f, 0.97f)) continue;
                float cover = 1f - Soft(thick * 0.25f, half, dist);
                float near = 0.5f + 0.5f * Math.Clamp((dy / (ry * q) + 1f) * 0.5f, 0f, 1f);       // 0.5 on the far side, 1 on the near side
                int at = y * fb.Width + x;
                fb.Pixels[at] = FrameBuffer.Blend(fb.Pixels[at], 208, 228, 250, alpha * cover * near);
            }
        }
    }

    /// <summary>One peek of an otter that is under the ice: every 6 to 15 s (cosmetic dice), a nose and a pair of eyes at the water, a look round, down again, with a ring each way.</summary>
    private void UpdatePeek(Otter o, float dt)
    {
        if (o.NextPeek <= 0f && o.PeekT < 0f) o.NextPeek = _force == "peek" ? 2.5f : 6f + 9f * (float)_fx.NextDouble();   // test dial PECKWORKS_OTTER=peek: dive, then peek 2.5 s later
        if (o.PeekT < 0f)
        {
            o.NextPeek -= dt;
            if (o.NextPeek <= 0f && o.T > 1.0 && o.M.Left > 3.4f && o.Alarm <= 0f)
            {
                o.PeekT = 0f; o.X = _hx; o.Y = _hy; o.Hd = _fx.Next(2) == 0 ? 0.3f : MathF.PI - 0.3f; o.PeekHd = o.Hd + (_fx.Next(2) == 0 ? 1f : -1f) * (0.7f + 0.5f * (float)_fx.NextDouble());
                o.NextPeek = 6f + 9f * (float)_fx.NextDouble();
                AddRing(_hx, _hy, 0.5f); _wob = MathF.Max(_wob, 0.6f);
                if (_log != null) Log($"{o.I} peeks");
            }
            return;
        }
        o.PeekT += dt;
        if (o.PeekT < 0.55f && _fx.NextDouble() < dt * 24) Drip(o.X + (float)(_fx.NextDouble() - 0.5) * 0.2f * _p, o.Y - 0.03f * _p, 0.35f);
        if (o.PeekT > 0.8f && o.PeekT < 1.7f) o.Hd = Turn.Toward(o.Hd, o.PeekHd, 1.4f, dt);        // the look round, eased
        if (o.PeekT >= 2.3f) { o.PeekT = -1f; AddRing(_hx, _hy, 0.4f); }
    }

    /// <summary>The front lip covers the part of a near-rim haul-out that is still in the water, then eases away as the otter clears the rim, and comes back after it has stood a while.</summary>
    private void UpdateLip(float dt)
    {
        bool near = false; float f = 0f;
        foreach (Otter o in _o) if (o.M.Doing == Doing.Haul && o.Near) { near = true; f = (float)(o.T / 1.7); }
        if (near) { _nearHold = 1.4f; _lipA = 1f - Soft(0.30f, 0.46f, f); return; }
        if (_nearHold > 0f) { _nearHold -= dt; _lipA = 0f; return; }
        _lipA = MathF.Min(1f, _lipA + dt / 0.4f);
    }

    private void AddBubble(float x, float y)
    {
        if (_bubN >= _bubs.Length) { _bubs[0] = _bubs[--_bubN]; }
        _bubs[_bubN++] = new Bub { X = x, Y = y, Age = 0f };
    }

    private void AddScratch(float x, float y)
    {
        if (_scrN >= _scr.Length) { Array.Copy(_scr, 1, _scr, 0, _scr.Length - 1); _scrN--; }
        bool joined = false;
        if (_scrN > 0) { Scr l = _scr[_scrN - 1]; joined = l.Age < 0.2f && MathF.Abs(l.X - x) < 0.5f * _p; }
        _scr[_scrN++] = new Scr { X = x, Y = y, Age = 0f, Joined = joined };
    }

    /// <summary>A droplet thrown up from (x, y): it rises, falls under gravity and is gone when it lands.</summary>
    private void Drip(float x, float y, float power)
    {
        if (_bitN >= _bits.Length) return;
        _bits[_bitN++] = new Bit
        {
            X = x, Y = y, H = 0.15f * _p * (float)_fx.NextDouble(),
            VX = (float)(_fx.NextDouble() - 0.5) * 0.9f * _p * power, VY = (float)(_fx.NextDouble() - 0.5) * 0.25f * _p * power,
            VH = (0.5f + 1.3f * (float)_fx.NextDouble()) * _p * power, Age = 0f, Max = 1.2f, Kind = 0,
        };
    }

    private void Splash(float x, float y)
    {
        _wob = 1f;
        for (int i = 0; i < 20; i++) Drip(x + (float)(_fx.NextDouble() - 0.5) * 0.2f * _p, y, 1.5f);
        Puff(x, y, 0.2f);
    }

    /// <summary>A soft puff of snow or ice dust at (x, y): grows and fades over half a second.</summary>
    private void Puff(float x, float y, float size, float height = -1f)
    {
        if (_bitN >= _bits.Length) return;
        _bits[_bitN++] = new Bit { X = x, Y = y, H = height < 0f ? 0.08f * _p : height, VX = (float)(_fx.NextDouble() - 0.5) * 0.3f * _p, VY = 0f, VH = 0.25f * _p, Age = 0f, Max = 0.35f + 0.5f * size, Kind = 1 };
    }

    private void UpdateBits(float dt)
    {
        for (int i = _bitN - 1; i >= 0; i--)
        {
            ref Bit b = ref _bits[i];
            b.Age += dt;
            b.X += b.VX * dt; b.Y += b.VY * dt; b.H += b.VH * dt;
            if (b.Kind == 0) b.VH -= 9.8f * _p * dt;
            bool dead = b.Age >= b.Max || (b.Kind == 0 && b.H < 0f);
            if (dead) _bits[i] = _bits[--_bitN];
        }
        _wob *= MathF.Exp(-0.85f * dt); _wobPh += 9f * dt; if (_wobPh > MathF.Tau * 50f) _wobPh -= MathF.Tau * 50f;
        for (int i = _ringN - 1; i >= 0; i--) { _rings[i].Age += dt; if (_rings[i].Age >= _rings[i].Max) _rings[i] = _rings[--_ringN]; }
        for (int i = _bubN - 1; i >= 0; i--) { _bubs[i].Age += dt; if (_bubs[i].Age >= 3.2f) _bubs[i] = _bubs[--_bubN]; }
        int keep = 0;
        for (int i = 0; i < _scrN; i++) { _scr[i].Age += dt; if (_scr[i].Age < 4.5f) _scr[keep++] = _scr[i]; }
        _scrN = keep;
    }

    /// <summary>The marks on the ice: scratches, bubbles, ripples. Drawn first of all, so every animal stands over them.</summary>
    private void DrawIce(FrameBuffer fb)
    {
        _back?.DrawSmooth(fb, _backX, _backY, 1f);
        _wobShown += (_wob - _wobShown) * MathF.Min(1f, 0.04f);   // eased in (about a tenth of a second at 60 a second) so a splash never switches the rocking on in one frame
        if (_wobShown > 0.03f)
        {
            // The water of the hole rocking: soft concentric ripples that swell and slide with the energy left by the last splash
            // (continuous pale arcs, thicker on the near side), plus a glint that swings from side to side.
            for (int k = 0; k < 3; k++)
            {
                float sc = 0.30f + 0.22f * k + 0.05f * MathF.Sin(_wobPh + k * 2.1f);
                SoftRing(fb, _hx, _hy + 0.04f * _hry, _hrx * sc * 1.4f, _hry * sc * 1.4f, 0.22f * _wobShown * (1f - 0.25f * k), 1.6f, true);
            }
            float gx = _hx + MathF.Sin(_wobPh * 0.7f) * _hrx * 0.45f;
            _pg!.Puff[0].DrawCentered(fb, gx, _hy - 0.25f * _hry, 0.5f * _wobShown);
        }
        if (false)
        {
            // The water of the hole rocking: three pale arcs across its lower half slide and swell with the energy left by the last
            // splash, plus a glint that swings from side to side (a wobbling reflection of the sky).
            for (int k = 0; k < 3; k++)
            {
                float sc = 0.30f + 0.2f * k + 0.05f * MathF.Sin(_wobPh + k * 2.1f);
                PointF pv = default; const int sg = 12;
                for (int j = 0; j <= sg; j++)
                {
                    float a = MathF.PI * (0.12f + 0.76f * j / sg);
                    var pt = new PointF(_hx + MathF.Cos(a) * _hrx * sc * 1.5f, _hy + 0.05f * _hry + MathF.Sin(a) * _hry * sc * 1.2f);
                    if (j > 0 && InHole(pt.X, pt.Y, 0.93f) && InHole(pv.X, pv.Y, 0.93f)) fb.Line(pv, pt, Color.FromArgb(150, 190, 240), 0.30f * _wob * (1f - 0.25f * k), 1f);
                    pv = pt;
                }
            }
            float gx = _hx + MathF.Sin(_wobPh * 0.7f) * _hrx * 0.45f;
            _pg!.Puff[0].DrawCentered(fb, gx, _hy - 0.25f * _hry, 0.5f * _wobShown);
        }
        for (int i = 1; i < _scrN; i++)
        {
            Scr a = _scr[i - 1], b = _scr[i];
            if (!b.Joined) continue;
            float al = 0.34f * (1f - b.Age / 4.5f);
            fb.Line(new PointF(a.X, a.Y), new PointF(b.X, b.Y), Color.FromArgb(238, 244, 255), al, 1.2f);
        }
        foreach (Bub b in _bubs.AsSpan(0, _bubN))
            _pg!.Droplet.DrawCentered(fb, b.X, b.Y - (float)Math.Sin(b.Age * 3) * 0.8f, 0.55f * (1f - b.Age / 3.2f));
        foreach (Ring r in _rings.AsSpan(0, _ringN))
        {
            float t = r.Age / r.Max;
            float rad = r.MaxR * 0.4f * _p * (1f - (1f - t) * (1f - t));
            bool inHole = MathF.Abs(r.X - _hx) < 2f && MathF.Abs(r.Y - _hy) < 2f;
            bool local = !inHole && HoleRadius(r.X, r.Y) < 1.0f;        // a ring that begins in the water (a head breaking it) stays in the water
            float rx = inHole ? MathF.Min(rad, _hrx * 0.9f) : rad, ry = inHole ? MathF.Min(rad * _r * (_hry / (_hrx * _r)), _hry * 0.85f) : rad * _r;
            if (rx < 1.5f) continue;
            float al = 0.62f * (1f - t) * (1f - t * 0.4f);
            // THREE SOFT RINGS (the review: dotted lines read as a diagram): the leading ring, and two behind it, closer and fainter, each a
            // continuous soft arc a pixel or two thick that thins as it widens, brighter on the near (lower) side, squashed like the hole.
            float thick = 2.0f - 0.9f * t;
            SoftRing(fb, r.X, r.Y, rx, ry, al, thick, local);
            if (rx > 4f) SoftRing(fb, r.X, r.Y, rx * 0.72f, ry * 0.72f, al * 0.6f, thick * 0.85f, local);
            if (rx > 7f) SoftRing(fb, r.X, r.Y, rx * 0.48f, ry * 0.48f, al * 0.35f, thick * 0.7f, local);
        }
    }

    /// <summary>Droplets and puffs: in front of everything on the snow.</summary>
    private void DrawBits(FrameBuffer fb)
    {
        OtterPages g = _pg!;
        foreach (Bit b in _bits.AsSpan(0, _bitN))
        {
            float t = b.Age / b.Max;
            if (b.Kind == 0) g.Droplet.DrawCentered(fb, b.X, b.Y - b.H, 0.9f * (1f - 0.5f * t));
            else g.Puff[Math.Min(2, (int)(t * 3f))].DrawCentered(fb, b.X, b.Y - b.H, 0.65f * (1f - t));
        }
    }
}
