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
        public float Width;                  // 1 = a normal ring; less = a thin one (a koi's back brushing the surface)
    }

    private readonly List<Ring> _rings = new();

    /// <summary>
    /// One wavelet of a bow wave: a small ring that starts at a swimmer's nose
    /// and spreads, but only its BACK half is drawn (Fx, Fy is the way the
    /// swimmer was heading when it was left behind). A moving nose leaves a
    /// string of these, and the back halves of rings left by a point moving
    /// faster than they spread line up into a V behind it: that is what a bow
    /// wave is (a boat's wake, a duck's), and it is the one thing in a calm
    /// pond that says where the surface is. Drawn thin and faint; the pond
    /// stays calm.
    /// </summary>
    private struct Wavelet
    {
        public float X, Y, Fx, Fy, Age, Strength;
    }
    private readonly List<Wavelet> _wake = new();
    private const int MostWake = 160;               // wavelets in the pond at once: about eight swimmers' worth at the longer life (96 at 1.5 s until round forty-six)
    private const float WakeStep = 0.012f;          // u swum between one wavelet and the next
    private const float WakeLife = 2.6f;            // seconds a wavelet lasts (1.5 until round forty-six, his line: "when things swim through water and break the surface for more than a second, they leave v shaped bow wakes": the V is the envelope of these rings, and at 1.5 s it was gone before it had drawn the arms)

    /// <summary>A ring worked out for this frame's draw: everything DrawRow needs, computed once per ring, not per row.</summary>
    private struct Prep
    {
        public float X, Y, Radius, Band, A, Outer, Inner;
        public int Y0, Y1;
        public float Fx, Fy;                        // a wavelet: only the half behind this direction is drawn
        public bool Half;
    }
    private readonly Prep[] _prep = new Prep[Most + MostWake];
    private int _prepCount;
    private FrameBuffer? _fb;
    private readonly Pond _pond;
    private readonly bool[] _where;      // open water and not under a branch
    private const int Most = 32;         // a cap, so a flurry of landings cannot pile up cost (48 rings cost 7 ms in one second at 1440p)

    public const float Squash = 0.86f;   // how much flatter than round a circle on the water looks from here

    /// <summary>
    /// Is the pond close to its cap? Decorations that ring the water all the
    /// time (a duck's wake, the fizz of ducklings' feet) ask this and skip a
    /// ring when it is true, so that the rings that mean something (a koi's
    /// gulp, a dragonfly's tap, a petal landing, a frog's splash) always find
    /// a slot. One adult duck's wake alone would otherwise hold eighteen of
    /// the thirty-two slots, and a drake and a hen together would refuse every
    /// other ring in the pond for the whole of their visit.
    /// </summary>
    public bool Crowded => _rings.Count >= Most - 12;

    // The ring's profile across its crest, worked out once: at distance d
    // from the crest (in band widths, negative inside) the light lift is
    // exp(-d^2) - 0.8 * exp(-(d + 1.6)^2), a bright crest with a dark trough
    // just inside it. Two exponentials per pixel for up to 48 rings was a
    // tenth of the frame; a table over d = -5 to 5 in steps of a hundredth
    // is the same curve for the cost of a lookup.
    private const float ProfileFrom = -5f, ProfileStep = 0.01f;
    private static readonly float[] Profile = Enumerable.Range(0, 1001).Select(i =>
    {
        float d = ProfileFrom + i * ProfileStep;
        return MathF.Exp(-d * d) - 0.8f * MathF.Exp(-(d + 1.6f) * (d + 1.6f));
    }).ToArray();

    public Ripples(Pond pond, bool[] where)
    {
        _pond = pond;
        _where = where;
    }

    /// <param name="strength">1 = a koi's gulp; 0.4 = a petal; 1.6 = a swallow hitting the water.</param>
    /// <param name="thin">A finer, slower, longer-lived ring: the surface barely disturbed (a koi's back brushing it from below).</param>
    public void Add(float x, float y, float strength, bool thin = false)
    {
        if (_rings.Count >= Most) return;
        int ix = (int)x, iy = (int)y;
        if (ix < 0 || iy < 0 || ix >= _pond.Width || iy >= _pond.Height || !_pond.Water[iy * _pond.Width + ix]) return;
        float u = _pond.U;
        _rings.Add(new Ring
        {
            X = x, Y = y, Strength = thin ? strength * 0.7f : strength,
            Life = thin ? 2.4f + 1.2f * strength : 1.6f + 1.4f * MathF.Min(1.5f, strength),
            Speed = thin ? u * (0.018f + 0.012f * strength) : u * (0.03f + 0.025f * MathF.Min(1.5f, strength)),
            Start = u * 0.003f,
            Width = thin ? 0.55f : 1f,
        });
    }

    /// <summary>
    /// A swimmer with its back at the surface calls this every frame; it
    /// leaves a wavelet of its bow wave every WakeStep of travel (so a thing
    /// hanging still leaves none). "travelled" is the caller's own counter of
    /// pixels swum since its last wavelet. x, y is the nose on the screen,
    /// heading the ground heading in radians, speed in pixels a second.
    /// Strength 0.5 is a koi at its cruise; the wave is decoration, so when the
    /// pond is near its ring cap, or the wake list is full, it is skipped.
    /// </summary>
    public void Wake(ref float travelled, float x, float y, float heading, float speed, float dt, float strength)
    {
        travelled += speed * dt;
        if (travelled < _pond.U * WakeStep) return;
        travelled = 0;
        if (strength < 0.02f || _wake.Count >= MostWake || Crowded) return;
        int ix = (int)x, iy = (int)y;
        if (ix < 0 || iy < 0 || ix >= _pond.Width || iy >= _pond.Height || !_pond.Water[iy * _pond.Width + ix]) return;
        _wake.Add(new Wavelet { X = x, Y = y, Fx = MathF.Cos(heading), Fy = MathF.Sin(heading), Strength = strength });
    }

    /// <summary>
    /// How hard the rings are pushing at each of a line of points this frame
    /// (the shore asks, with the points along the bank): for every ring whose
    /// crest is within "band" pixels of a point, its strength, faded as the
    /// ring fades, is added to that point's entry (capped at 1.5). A koi's
    /// gulp near the sand sends its ring up the beach as a wash; a ring far out
    /// never arrives. Cheap: a few rings by a few hundred points.
    /// </summary>
    public void Surge(PointF[] at, float[] into, float band)
    {
        Array.Clear(into);
        foreach (Ring r in _rings)
        {
            float life = r.Age / r.Life;
            float a = r.Strength * (1 - life) * (1 - life);
            if (a < 0.01f) continue;
            float radius = r.Start + r.Speed * r.Age;
            for (int k = 0; k < at.Length; k++)
            {
                float dx = at[k].X - r.X, dy = (at[k].Y - r.Y) / Squash;
                float d = MathF.Sqrt(dx * dx + dy * dy) - radius;
                if (d < -band || d > band) continue;
                float f = 1 - MathF.Abs(d) / band;
                into[k] = MathF.Min(1.5f, into[k] + a * f * f);
            }
        }
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
        for (int i = _wake.Count - 1; i >= 0; i--)
        {
            Wavelet v = _wake[i];
            v.Age += dt;
            if (v.Age >= WakeLife) { _wake.RemoveAt(i); continue; }
            _wake[i] = v;
        }
    }

    /// <summary>
    /// Paints every ring. The rows of the picture are split into bands and the
    /// bands are painted on separate cores; each band paints, row by row, the
    /// rings that cross it, in the rings' own order. No two bands share a row,
    /// so no two cores ever touch the same pixel and the picture is, pixel for
    /// pixel, the one the old ring-by-ring loop painted (checked by a pixel
    /// diff at two seeds). A frame with a ring or two is painted straight
    /// through: handing work to the thread pool costs more than it saves then.
    /// At the 2048 draw width the rings were 1.2 ms of a 9.7 ms frame drawn one
    /// after another, one core busy and the rest idle.
    /// </summary>
    public void Draw(FrameBuffer fb)
    {
        _prepCount = 0;
        int rows = 0;
        foreach (Ring r in _rings)
        {
            float life = r.Age / r.Life;
            float radius = r.Start + r.Speed * r.Age;
            float band = MathF.Max(1.2f, _pond.U * 0.0025f * (1 + 2 * life) * r.Width);    // the ring gets wider as it spreads
            // Strong at the start, fading out; a gentle fade-in over the first moment so it does not pop.
            float a = r.Strength * 0.22f * (1 - life) * (1 - life) * MathF.Min(1, r.Age * 8);
            if (a < 0.005f) continue;
            float outer = radius + band * 2.5f, inner = MathF.Max(0, radius - band * 2.5f);
            float ry = outer * Squash;
            int y0 = Math.Max(0, (int)(r.Y - ry)), y1 = Math.Min(fb.Height - 1, (int)(r.Y + ry));
            if (y1 < y0) continue;
            _prep[_prepCount++] = new Prep { X = r.X, Y = r.Y, Radius = radius, Band = band, A = a, Outer = outer, Inner = inner, Y0 = y0, Y1 = y1 };
            rows += y1 - y0 + 1;
        }
        // The wavelets of the bow waves: thin, slow, faint rings, the back half of each.
        float u = _pond.U;
        foreach (Wavelet v in _wake)
        {
            float life = v.Age / WakeLife;
            float radius = u * 0.002f + u * 0.022f * v.Age;
            float band = MathF.Max(1.2f, u * 0.0014f * (1 + 2 * life));
            float a = v.Strength * 0.9f * (1 - life) * MathF.Sqrt(1 - life) * MathF.Min(1, v.Age * 8);   // a koi at its cruise starts at 0.45 (0.3 until round forty-six, fading as the square: the arms of the V never showed), a gulp's ring at 0.22, but on a band half as wide
            if (a < 0.005f) continue;
            float outer = radius + band * 2.5f, inner = MathF.Max(0, radius - band * 2.5f);
            float ry = outer * Squash;
            int y0 = Math.Max(0, (int)(v.Y - ry)), y1 = Math.Min(fb.Height - 1, (int)(v.Y + ry));
            if (y1 < y0) continue;
            _prep[_prepCount++] = new Prep { X = v.X, Y = v.Y, Radius = radius, Band = band, A = a, Outer = outer, Inner = inner, Y0 = y0, Y1 = y1, Fx = v.Fx, Fy = v.Fy, Half = true };
            rows += y1 - y0 + 1;
        }
        if (_prepCount == 0) return;
        _fb = fb;
        const int BandRows = 32;
        if (rows < 400)
        {
            // Little to do: one core, no hand-off.
            for (int k = 0; k < _prepCount; k++)
                for (int y = _prep[k].Y0; y <= _prep[k].Y1; y++) DrawRow(fb, k, y);
        }
        else
        {
            int bands = (fb.Height + BandRows - 1) / BandRows;
            Parallel.For(0, bands, band =>
            {
                FrameBuffer f = _fb!;
                int by0 = band * BandRows, by1 = Math.Min(f.Height - 1, by0 + BandRows - 1);
                for (int k = 0; k < _prepCount; k++)
                {
                    int y0 = Math.Max(by0, _prep[k].Y0), y1 = Math.Min(by1, _prep[k].Y1);
                    for (int y = y0; y <= y1; y++) DrawRow(f, k, y);
                }
            });
        }
        _fb = null;
    }

    /// <summary>One row of one prepared ring: the two arcs it makes on that row.</summary>
    private void DrawRow(FrameBuffer fb, int k, int y)
    {
        ref Prep p = ref _prep[k];
        // In the ring's own round frame, this row is dy away from the middle.
        float dy = (y + 0.5f - p.Y) / Squash;
        float xo2 = p.Outer * p.Outer - dy * dy;
        if (xo2 <= 0) return;
        float xo = MathF.Sqrt(xo2);
        float xi2 = p.Inner * p.Inner - dy * dy;
        float xi = xi2 > 0 ? MathF.Sqrt(xi2) : 0;
        // Two spans on this row: the left arc and the right arc (one span if the row is past the hole).
        Span(fb.Pixels, fb.Width, y, p.X - xo, p.X - xi, p.X, dy, p.Radius, p.Band, p.A, p.Half, p.Fx, p.Fy);
        Span(fb.Pixels, fb.Width, y, p.X + xi, p.X + xo, p.X, dy, p.Radius, p.Band, p.A, p.Half, p.Fx, p.Fy);
    }

    private void Span(uint[] px, int w, int y, float from, float to, float cx, float dy, float radius, float band, float a, bool half, float fx, float fy)
    {
        int x0 = Math.Max(0, (int)from), x1 = Math.Min(w - 1, (int)to);
        int row = y * w;
        float ahead = radius * 0.2f;                                         // a wavelet: nothing further ahead of its origin than this is drawn
        for (int x = x0; x <= x1; x++)
        {
            int i = row + x;
            if (!_where[i]) continue;
            float dx = x + 0.5f - cx;
            if (half)
            {
                // A wavelet is drawn only inside the V behind its swimmer (round forty-six, his line: "they leave v shaped
                // bow wakes"): nothing ahead of its origin, and nothing further to the side than a wedge of about 35
                // degrees either side of straight back allows (a little more near the origin, so the arc is not cut to
                // a point). Drawn as whole half rings the wavelets nested into a fan of arcs; clipped to the wedge they
                // are the wake every photo shows, the arms of the V with the cross waves between them.
                float back = -(dx * fx + dy * fy), side = MathF.Abs(dx * fy - dy * fx);
                if (back < -ahead) continue;
                if (side > back * 0.7f + radius * 0.3f) continue;
            }
            float d = (MathF.Sqrt(dx * dx + dy * dy) - radius) / band;     // 0 on the crest, negative inside
            int k = (int)((d - ProfileFrom) / ProfileStep);
            if (k < 0 || k >= Profile.Length) continue;                  // too far from the crest to show
            float lift = Profile[k] * a;                                 // crest (light) outside, trough (dark) just inside
            if (lift > 0.003f) px[i] = FrameBuffer.Blend(px[i], 226, 238, 240, lift);
            else if (lift < -0.003f) px[i] = FrameBuffer.Blend(px[i], 6, 26, 30, -lift);
        }
    }
}
