using Peckworks.Screensavers.Core;

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
        for (int i = _ringN - 1; i >= 0; i--) { _rings[i].Age += dt; if (_rings[i].Age >= _rings[i].Max) _rings[i] = _rings[--_ringN]; }
        for (int i = _bubN - 1; i >= 0; i--) { _bubs[i].Age += dt; if (_bubs[i].Age >= 3.2f) _bubs[i] = _bubs[--_bubN]; }
        int keep = 0;
        for (int i = 0; i < _scrN; i++) { _scr[i].Age += dt; if (_scr[i].Age < 4.5f) _scr[keep++] = _scr[i]; }
        _scrN = keep;
    }

    /// <summary>The marks on the ice: scratches, bubbles, ripples. Drawn first of all, so every animal stands over them.</summary>
    private void DrawIce(FrameBuffer fb)
    {
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
            float rx = inHole ? MathF.Min(rad, _hrx * 0.9f) : rad, ry = inHole ? MathF.Min(rad * _r * (_hry / (_hrx * _r)), _hry * 0.85f) : rad * _r;
            if (rx < 1.5f) continue;
            float al = 0.55f * (1f - t);
            PointF prev = default;
            const int seg = 22;
            for (int k = 0; k <= seg; k++)
            {
                float a = MathF.Tau * k / seg;
                var pt = new PointF(r.X + MathF.Cos(a) * rx, r.Y + MathF.Sin(a) * ry);
                if (k > 0) fb.Line(prev, pt, Color.FromArgb(190, 214, 244), al, 1f);
                prev = pt;
            }
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
