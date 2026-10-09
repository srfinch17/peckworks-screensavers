using Peckworks.Screensavers.Core;

namespace SakuraPond;

/// <summary>
/// Rings spreading on the water wherever something touches it: a petal
/// landing, a koi gulping at the surface, a dragonfly dipping its tail, a
/// swallow drinking, the snake's head.
///
/// FEYNMAN VERSION: drop a pebble in a bath and watch. A ring of raised
/// water runs outward, with a little trough just inside it; the ring gets
/// wider and fainter as it spreads, and is gone in a couple of seconds. On
/// the screen the raised crest catches the sky and looks a touch lighter,
/// the trough a touch darker. We draw exactly that: a thin light band with a
/// dark band just inside, on an oval (the pond is seen at a slant, so a
/// circle on the water looks a little flattened), on open water only.
///
/// Only the ring's own band of pixels is visited, row by row, never the whole
/// square around it: a big ring at 4K would otherwise be a quarter of a
/// million pixels for a line a few pixels wide.
/// </summary>
internal sealed class Ripples
{
    private struct Ring
    {
        public float X, Y, Age, Life, Speed, Strength, Start;
    }

    private readonly List<Ring> _rings = new();
    private readonly Pond _pond;
    private readonly bool[] _where;      // open water and not under a branch
    private const int Most = 48;         // a cap, so a flurry of landings cannot pile up cost

    public const float Squash = 0.86f;   // how much flatter than round a circle on the water looks from here

    public Ripples(Pond pond, bool[] where)
    {
        _pond = pond;
        _where = where;
    }

    /// <param name="strength">1 = a koi's gulp; 0.4 = a petal; 1.6 = a swallow hitting the water.</param>
    public void Add(float x, float y, float strength)
    {
        if (_rings.Count >= Most) return;
        int ix = (int)x, iy = (int)y;
        if (ix < 0 || iy < 0 || ix >= _pond.Width || iy >= _pond.Height || !_pond.Water[iy * _pond.Width + ix]) return;
        float u = _pond.U;
        _rings.Add(new Ring
        {
            X = x, Y = y, Strength = strength,
            Life = 1.6f + 1.4f * MathF.Min(1.5f, strength),
            Speed = u * (0.03f + 0.025f * MathF.Min(1.5f, strength)),
            Start = u * 0.003f,
        });
    }

    public void Update(float dt)
    {
        for (int i = _rings.Count - 1; i >= 0; i--)
        {
            Ring r = _rings[i];
            r.Age += dt;
            if (r.Age >= r.Life) { _rings.RemoveAt(i); continue; }
            _rings[i] = r;
        }
    }

    public void Draw(FrameBuffer fb)
    {
        foreach (Ring r in _rings) DrawRing(fb, r);
    }

    private void DrawRing(FrameBuffer fb, in Ring r)
    {
        float life = r.Age / r.Life;
        float radius = r.Start + r.Speed * r.Age;
        float band = MathF.Max(1.2f, _pond.U * 0.0025f * (1 + 2 * life));    // the ring gets wider as it spreads
        // Strong at the start, fading out; a gentle fade-in over the first moment so it does not pop.
        float a = r.Strength * 0.22f * (1 - life) * (1 - life) * MathF.Min(1, r.Age * 8);
        if (a < 0.005f) return;
        float outer = radius + band * 2.5f, inner = MathF.Max(0, radius - band * 2.5f);
        float ry = outer * Squash;
        int w = fb.Width;
        int y0 = Math.Max(0, (int)(r.Y - ry)), y1 = Math.Min(fb.Height - 1, (int)(r.Y + ry));
        uint[] px = fb.Pixels;
        for (int y = y0; y <= y1; y++)
        {
            // In the ring's own round frame, this row is dy away from the middle.
            float dy = (y + 0.5f - r.Y) / Squash;
            float xo2 = outer * outer - dy * dy;
            if (xo2 <= 0) continue;
            float xo = MathF.Sqrt(xo2);
            float xi2 = inner * inner - dy * dy;
            float xi = xi2 > 0 ? MathF.Sqrt(xi2) : 0;
            // Two spans on this row: the left arc and the right arc (one span if the row is past the hole).
            Span(fb, px, w, y, r.X - xo, r.X - xi, r, dy, radius, band, a);
            Span(fb, px, w, y, r.X + xi, r.X + xo, r, dy, radius, band, a);
        }
    }

    private void Span(FrameBuffer fb, uint[] px, int w, int y, float from, float to, in Ring r, float dy, float radius, float band, float a)
    {
        int x0 = Math.Max(0, (int)from), x1 = Math.Min(w - 1, (int)to);
        int row = y * w;
        for (int x = x0; x <= x1; x++)
        {
            int i = row + x;
            if (!_where[i]) continue;
            float dx = x + 0.5f - r.X;
            float d = (MathF.Sqrt(dx * dx + dy * dy) - radius) / band;     // 0 on the crest, negative inside
            // Crest (light) on the outside, trough (dark) just inside it.
            float crest = MathF.Exp(-d * d), trough = MathF.Exp(-(d + 1.6f) * (d + 1.6f));
            float lift = (crest - 0.8f * trough) * a;
            if (lift > 0.003f) px[i] = FrameBuffer.Blend(px[i], 226, 238, 240, lift);
            else if (lift < -0.003f) px[i] = FrameBuffer.Blend(px[i], 6, 26, 30, -lift);
        }
    }
}
