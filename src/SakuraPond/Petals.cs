using Peckworks.Screensavers.Core;
using Peckworks.Screensavers.Core.Sakura;

namespace SakuraPond;

/// <summary>
/// Cherry petals, seen from above: falling from the blossom, landing on the
/// water with a tiny ring, then floating, drifting slowly and gathering
/// against the banks.
///
/// FEYNMAN VERSION: from above, a falling petal is a story about HEIGHT.
/// Each petal knows the spot on the ground or water below it, and how high
/// above that spot it is. We see it from above and a little in front, so the
/// higher it is, the further up the screen it shows from that spot, and the
/// nearer to us it is, so the bigger it looks. As it falls it slides down the
/// screen toward its spot and shrinks a little, while its faint shadow, cast
/// by the sun up and to the left, slides in from the other side to meet it.
/// The moment they meet, it has landed.
///
/// Its fall is the familiar one from the other sakura savers: it sways, spins
/// and tumbles (the tumble is faked by squashing its width, see PetalField).
/// On the water it lies flat: no tumble, a slow turn, carried by a gentle
/// drift that circles the pond and is nudged now and then by the breeze.
/// </summary>
internal sealed class Petals
{
    private struct Falling
    {
        public float X, Y;              // the spot below it, screen pixels
        public float Height;            // how high above that spot, in u
        public float Size, Angle, Spin, Flip, FlipSpeed, Sway, SwayFreq, SwayAmp, Pink, Fall;
    }

    private struct Floating
    {
        public float X, Y, Age, Life;
        public float R;               // half its size, screen pixels: how close a body may come before it is shoved (PushAside)
        public float Vx, Vy;          // a shove sent it gliding this fast, screen pixels a second, dying away over a second or so
        public Sprite Look;           // the petal lying flat, made once (see MakeLook)
    }

    private readonly Pond _pond;
    private readonly Ripples _ripples;
    private readonly bool[] _waterInView;
    private readonly Random _rng;
    private readonly float _u;
    private readonly List<Falling> _falling = new();
    private readonly List<Floating> _floating = new();
    private readonly int _fallingCount, _floatingMost;
    private readonly (Color Pale, Color Deep) _colors = (Color.FromArgb(255, 246, 249), Color.FromArgb(255, 176, 199));
    private readonly Wind _wind;

    private const float Lift = Light.Lift;
    private const float ShadowX = Light.ShadowX, ShadowY = Light.ShadowY;
    private const float BranchHeight = 0.38f;   // u: below this a falling petal is under the branches, which hide it

    public Petals(Pond pond, Ripples ripples, bool[] waterInView, float amount, Wind wind, Random rng)
    {
        _pond = pond;
        _ripples = ripples;
        _waterInView = waterInView;
        _rng = rng;
        _wind = wind;
        _u = pond.U;
        // 1 on a 16:9 screen, more on a wider one. Capped: a tall screen has a
        // lot of area for its size unit, and uncapped its pond looked snowed on.
        float area = MathF.Min(1.6f, pond.Width * (float)pond.Height / (_u * _u * 16f / 9f));
        _fallingCount = (int)Math.Round(26 * amount * area);
        _floatingMost = (int)Math.Round(260 * area);
        // The pond starts with petals already floating on it, spread out.
        int start = (int)(_floatingMost * 0.55f);
        for (int i = 0, tries = 0; i < start && tries < start * 20; tries++)
        {
            float x = (float)rng.NextDouble() * pond.Width, y = (float)rng.NextDouble() * pond.Height;
            if (!pond.Water[(int)y * pond.Width + (int)x]) continue;
            _floating.Add(NewFloating(x, y, age: (float)rng.NextDouble() * 120));
            i++;
        }
        for (int i = 0; i < _fallingCount; i++) _falling.Add(NewFalling(scatter: true));
    }

    private Floating NewFloating(float x, float y, float age = 0, float size = -1, float angle = -1, float pink = -1)
    {
        // The dice in the same order as ever (life, then size, angle, pink), so a
        // seed still gives the scene it gave before this change (old-vs-new timing
        // on one seed depends on it).
        float life = 180 + 240 * (float)_rng.NextDouble();
        if (size < 0) size = _u * (0.011f + 0.006f * (float)_rng.NextDouble());
        return new()
        {
            X = x, Y = y, Age = age, R = size * 0.5f,
            Life = life,
            Look = MakeLook(size,
                            angle >= 0 ? angle : (float)(_rng.NextDouble() * MathF.Tau),
                            pink >= 0 ? pink : (float)_rng.NextDouble()),
        };
    }

    /// <summary>
    /// The stamp for a petal lying flat on the water. A floating petal does
    /// not tumble and barely turns, so its look never changes: work it out
    /// once, keep it, and stamp it every frame (much quicker than working out
    /// the petal's shape pixel by pixel each time).
    ///
    /// How to get a see-through stamp from a painter that only paints ONTO
    /// something: paint the petal once on black and once on white. Where the
    /// petal is solid the two come out the same; where it is clear they
    /// differ by the full black-to-white step; in between, by how see-through
    /// it is. So the difference tells us how solid each pixel is, and the
    /// black copy (divided by that) gives its colour.
    /// </summary>
    private Sprite MakeLook(float size, float angle, float pink)
    {
        int n = (int)MathF.Ceiling(size) + 4;
        var black = new FrameBuffer(n, n);
        var white = new FrameBuffer(n, n);
        Array.Fill(white.Pixels, 0x00FFFFFFu);
        foreach (FrameBuffer f in (ReadOnlySpan<FrameBuffer>)[black, white])
            PetalField.PaintPetal(f, n / 2f, n / 2f, size, angle, 1f, pink, 1f, _colors, (0.97f, 0.97f, 0.97f), squash: Ripples.Squash);
        var argb = new uint[n * n];
        for (int i = 0; i < argb.Length; i++)
        {
            uint b = black.Pixels[i], w = white.Pixels[i];
            int clear = ((int)((w >> 16) & 0xFF) - (int)((b >> 16) & 0xFF) + (int)((w >> 8) & 0xFF) - (int)((b >> 8) & 0xFF) + (int)(w & 0xFF) - (int)(b & 0xFF)) / 3;
            int a = Math.Clamp(255 - clear, 0, 255);
            if (a == 0) continue;
            int r = Math.Min(255, (int)((b >> 16) & 0xFF) * 255 / a), g = Math.Min(255, (int)((b >> 8) & 0xFF) * 255 / a), bl = Math.Min(255, (int)(b & 0xFF) * 255 / a);
            argb[i] = (uint)(a << 24 | r << 16 | g << 8 | bl);
        }
        return Sprite.FromPixels(n, n, argb);
    }

    /// <summary>
    /// A new falling petal: most let go of a blossom on the branches over the
    /// pond; some blow in from trees out of sight, so they come in from high
    /// up, above the top of the screen.
    /// </summary>
    private Falling NewFalling(bool scatter)
    {
        float x, y, height;
        IReadOnlyList<PointF> spots = _pond.BlossomSpots;
        if (spots.Count > 0 && _rng.NextDouble() < 0.7)
        {
            PointF s = spots[_rng.Next(spots.Count)];
            height = 0.22f + 0.14f * (float)_rng.NextDouble();
            x = s.X;
            y = s.Y + height * Lift * _u;                 // the spot below the blossom, further down the screen
        }
        else
        {
            height = 0.5f + 0.3f * (float)_rng.NextDouble();
            x = (float)_rng.NextDouble() * _pond.Width;
            y = (float)_rng.NextDouble() * _pond.Height;
        }
        if (scatter) height *= (float)_rng.NextDouble();  // at the start, some are already partway down
        return new Falling
        {
            X = x, Y = y, Height = height,
            Size = _u * (0.011f + 0.006f * (float)_rng.NextDouble()),
            Angle = (float)(_rng.NextDouble() * MathF.Tau),
            Spin = ((float)_rng.NextDouble() - 0.5f) * 2.4f,
            Flip = (float)(_rng.NextDouble() * MathF.Tau),
            FlipSpeed = 1.5f + 3f * (float)_rng.NextDouble(),
            Sway = (float)(_rng.NextDouble() * MathF.Tau),
            SwayFreq = 0.6f + 0.7f * (float)_rng.NextDouble(),
            SwayAmp = 0.012f + 0.014f * (float)_rng.NextDouble(),
            Pink = (float)_rng.NextDouble(),
            Fall = 0.045f + 0.03f * (float)_rng.NextDouble(),   // u per second: a slow, drifting fall
        };
    }

    /// <summary>The breeze: a gentle push to the right that rises and dies away with the shared wind's swell, so petals gust when the grass does.</summary>
    private float Breeze() => _u * (0.006f + 0.008f * _wind.Swell);

    public void Update(float dt)
    {
        float breeze = Breeze();
        var c = _pond.Centre;

        for (int i = 0; i < _falling.Count; i++)
        {
            Falling p = _falling[i];
            p.Height -= p.Fall * dt * (0.75f + 0.5f * (1 - MathF.Abs(MathF.Cos(p.Flip))));   // edge-on petals cut through the air faster
            p.Sway += p.SwayFreq * MathF.Tau * dt;
            p.Sway %= MathF.Tau;
            p.X += (breeze * 2.5f + MathF.Cos(p.Sway) * p.SwayAmp * _u * 2) * dt;
            p.Angle = (p.Angle + p.Spin * dt) % MathF.Tau;
            p.Flip = (p.Flip + p.FlipSpeed * dt) % MathF.Tau;
            if (p.Height <= 0)
            {
                Land(p);
                p = NewFalling(scatter: false);
            }
            _falling[i] = p;
        }

        for (int i = _floating.Count - 1; i >= 0; i--)
        {
            Floating f = _floating[i];
            f.Age += dt;
            if (f.Age > f.Life) { _floating.RemoveAt(i); continue; }
            // The drift: a slow turn round the pond's middle, plus the breeze.
            float dx = f.X - c.X, dy = (f.Y - c.Y) / Ripples.Squash;
            float r = MathF.Max(1, MathF.Sqrt(dx * dx + dy * dy));
            float swirl = _u * 0.006f;
            float vx = -dy / r * swirl + breeze * 0.6f + f.Vx, vy = dx / r * swirl * Ripples.Squash + f.Vy;
            float nx = f.X + vx * dt, ny = f.Y + vy * dt;
            // A shove (PushAside) sent it gliding; it slows over a second or so, as a petal on water does.
            float slow = MathF.Exp(-dt * 3f);
            f.Vx *= slow; f.Vy *= slow;
            int ix = (int)nx, iy = (int)ny;
            // It drifts only while there is water ahead; against the bank (or a pad) it stays, the way petals pile up at the edges.
            if (ix >= 0 && iy >= 0 && ix < _pond.Width && iy < _pond.Height && _pond.Water[iy * _pond.Width + ix])
            {
                f.X = nx; f.Y = ny;
            }
            _floating[i] = f;
        }
    }

    /// <summary>A falling petal reaches its spot: on open water it floats (with a little ring); anywhere else it is gone.</summary>
    private void Land(in Falling p)
    {
        int ix = (int)p.X, iy = (int)p.Y;
        if (ix < 0 || iy < 0 || ix >= _pond.Width || iy >= _pond.Height || !_pond.Water[iy * _pond.Width + ix]) return;
        _ripples.Add(p.X, p.Y, 0.35f);
        if (_floating.Count >= _floatingMost)
        {
            // Full: the oldest petal starts sinking to make room.
            int oldest = 0;
            for (int k = 1; k < _floating.Count; k++) if (_floating[k].Age > _floating[oldest].Age) oldest = k;
            Floating o = _floating[oldest];
            o.Life = MathF.Min(o.Life, o.Age + 6);
            _floating[oldest] = o;
        }
        _floating.Add(NewFloating(p.X, p.Y, 0, p.Size, p.Angle, p.Pink));
    }

    /// <summary>The nearest floating petal within "within" pixels of (x, y), screen pixels. False if none.</summary>
    public bool NearestFloating(float x, float y, float within, out PointF at)
    {
        int best = Nearest(x, y, within);
        at = best < 0 ? default : new PointF(_floating[best].X, _floating[best].Y);
        return best >= 0;
    }

    /// <summary>A koi's mouth closed here: the nearest petal within reach is eaten (it sinks away over a moment).</summary>
    public void Take(float x, float y, float within)
    {
        int i = Nearest(x, y, within);
        if (i < 0) return;
        Floating f = _floating[i];
        f.Life = MathF.Min(f.Life, f.Age + 0.6f);
        _floating[i] = f;
    }

    /// <summary>
    /// Something is passing ON the surface: every floating petal in its way
    /// is shoved aside, out from the body's edge, and sent gliding a little,
    /// so nothing is ever drawn through a petal. The body is its drawn spine
    /// (screen pixels, nose first) with half its width at each point. For a
    /// koi, which mostly swims UNDER the petals, "depth" says how deep each
    /// point's back is (u): only the pieces up at the surface (shallower than
    /// "surface") shove; the rest pass beneath, and the petals are painted
    /// over them.
    ///
    /// FEYNMAN VERSION: for each petal, find the nearest piece of the body.
    /// If the petal's edge is inside the body's edge there, move the petal
    /// straight out until it just clears, and give it a little speed that way
    /// (Update lets it coast and slow). Against the bank, where straight out
    /// is land, it is slid along the body instead; and only if that is land
    /// too does it stay put.
    /// </summary>
    public void PushAside(ReadOnlySpan<PointF> spine, ReadOnlySpan<float> halfWidth, ReadOnlySpan<float> depth = default, float surface = 0)
    {
        if (_floating.Count == 0 || spine.Length < 2) return;
        // The body's box, so most petals are dismissed with four compares.
        float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue, wide = 0;
        for (int i = 0; i < spine.Length; i++)
        {
            x0 = MathF.Min(x0, spine[i].X); x1 = MathF.Max(x1, spine[i].X);
            y0 = MathF.Min(y0, spine[i].Y); y1 = MathF.Max(y1, spine[i].Y);
            wide = MathF.Max(wide, halfWidth[i]);
        }
        float margin = wide + _u * 0.012f;
        for (int k = 0; k < _floating.Count; k++)
        {
            Floating f = _floating[k];
            if (f.X < x0 - margin || f.X > x1 + margin || f.Y < y0 - margin || f.Y > y1 + margin) continue;
            // The nearest piece of the body, and the body's half width there.
            float bestD2 = float.MaxValue, px = 0, py = 0, bw = 0, tx = 0, ty = 0;
            for (int i = 0; i < spine.Length - 1; i++)
            {
                if (!depth.IsEmpty && (depth[i] > surface || depth[i + 1] > surface)) continue;   // this piece is down under the petals
                PointF a = spine[i], b = spine[i + 1];
                float ex = b.X - a.X, ey = b.Y - a.Y, l2 = ex * ex + ey * ey;
                float t = l2 > 1e-4f ? Math.Clamp(((f.X - a.X) * ex + (f.Y - a.Y) * ey) / l2, 0, 1) : 0;
                float qx = a.X + ex * t, qy = a.Y + ey * t;
                float dx = f.X - qx, dy = f.Y - qy, d2 = dx * dx + dy * dy;
                if (d2 < bestD2) { bestD2 = d2; px = qx; py = qy; bw = halfWidth[i] + (halfWidth[i + 1] - halfWidth[i]) * t; tx = ex; ty = ey; }
            }
            if (bestD2 == float.MaxValue) continue;
            float want = bw + f.R, d = MathF.Sqrt(bestD2);
            if (d >= want) continue;
            // Out from the body's edge; a petal right on the spine goes out sideways.
            float ox, oy;
            if (d > 0.5f) { ox = (f.X - px) / d; oy = (f.Y - py) / d; }
            else { float l = MathF.Max(1e-3f, MathF.Sqrt(tx * tx + ty * ty)); ox = -ty / l; oy = tx / l; }
            float by = want - d + 0.5f;
            if (!OnWater(f.X + ox * by, f.Y + oy * by))
            {
                // The bank is that way: slide along the body instead, whichever way is water.
                float l = MathF.Max(1e-3f, MathF.Sqrt(tx * tx + ty * ty)), sx = tx / l, sy = ty / l;
                if (OnWater(f.X + sx * by, f.Y + sy * by)) { ox = sx; oy = sy; }
                else if (OnWater(f.X - sx * by, f.Y - sy * by)) { ox = -sx; oy = -sy; }
                else continue;
            }
            f.X += ox * by; f.Y += oy * by;
            float glide = _u * 0.06f;
            f.Vx = ox * glide; f.Vy = oy * glide;
            _floating[k] = f;
        }
    }

    private bool OnWater(float x, float y)
    {
        int ix = (int)x, iy = (int)y;
        return ix >= 0 && iy >= 0 && ix < _pond.Width && iy < _pond.Height && _pond.Water[iy * _pond.Width + ix];
    }

    private int Nearest(float x, float y, float within)
    {
        int best = -1;
        float bestD = within * within;
        for (int i = 0; i < _floating.Count; i++)
        {
            Floating f = _floating[i];
            if (f.Life - f.Age < 2) continue;                                    // already sinking
            float dx = f.X - x, dy = f.Y - y, d = dx * dx + dy * dy;
            if (d < bestD) { bestD = d; best = i; }
        }
        return best;
    }

    /// <summary>The petals lying on the water. Drawn after the koi (they float above them) and before the falling ones.</summary>
    public void DrawFloating(FrameBuffer fb)
    {
        foreach (Floating f in _floating)
        {
            // Fade in over the first moment (no pop), and out at the end as it sinks.
            float a = MathF.Min(1, f.Age * 4) * MathF.Min(1, (f.Life - f.Age) / 6f) * 0.92f;
            if (a <= 0) continue;
            f.Look.DrawCentered(fb, MathF.Round(f.X), MathF.Round(f.Y), a, _waterInView);
        }
    }

    /// <summary>The falling petals and their shadows. Drawn last: they are the nearest things to us.</summary>
    public void DrawFalling(FrameBuffer fb)
    {
        foreach (Falling p in _falling.OrderBy(q => q.Height))
        {
            float h = p.Height * _u;
            // The shadow: a soft pale-dark smudge on the water or ground, sliding in to meet the petal.
            float sx = p.X + h * ShadowX, sy = p.Y + h * ShadowY;
            float shadowA = 0.22f * (1 - MathF.Min(1, p.Height / 0.6f));
            if (shadowA > 0.01f)
                Shadow(fb, sx, sy, p.Size * 0.45f * (1 + p.Height), shadowA);
            float near = 1 + p.Height * 0.9f;                 // higher up = nearer to us = bigger
            float fade = MathF.Min(1, (0.85f - p.Height) * 3);    // the ones from off screen fade in from high up
            if (fade <= 0) continue;
            PetalField.PaintPetal(fb, p.X + MathF.Sin(p.Sway) * p.SwayAmp * _u, p.Y - h * Lift, p.Size * near,
                p.Angle + 0.5f * MathF.Sin(p.Sway), MathF.Cos(p.Flip), p.Pink, 0.95f * fade, _colors, (1f, 1f, 1f),
                onlyWhere: p.Height > BranchHeight ? null : _pond.Open);
        }
    }

    private void Shadow(FrameBuffer fb, float x, float y, float r, float alpha)
    {
        int x0 = Math.Max(0, (int)(x - r)), x1 = Math.Min(fb.Width - 1, (int)(x + r));
        int y0 = Math.Max(0, (int)(y - r)), y1 = Math.Min(fb.Height - 1, (int)(y + r));
        for (int py = y0; py <= y1; py++)
            for (int px = x0; px <= x1; px++)
            {
                int i = py * fb.Width + px;
                if (!_pond.Open[i]) continue;
                float dx = (px + 0.5f - x) / r, dy = (py + 0.5f - y) / (r * Ripples.Squash);
                float q = 1 - (dx * dx + dy * dy);
                if (q <= 0) continue;
                fb.Pixels[i] = FrameBuffer.Blend(fb.Pixels[i], 10, 26, 28, alpha * q);
            }
    }
}
