using System.Drawing.Drawing2D;
using Peckworks.Screensavers.Core;

namespace CabinByStream;

/// <summary>
/// Trout holding in the stream.
///
/// A river fish does not cruise about like a goldfish: it faces upstream
/// and holds its place against the current, drifting slowly back, then
/// darting forward a body length or two to take its spot again. So each
/// fish here faces up the screen (the water flows toward the viewer), drifts
/// down slowly, and every few seconds darts up, wagging harder as it goes.
/// Now and then it slides a little to one side of the channel.
///
/// FEYNMAN VERSION of the drawing: a fish swims by sending a wave down its
/// body, head to tail, like shaking a rope. To draw that, the fish is
/// painted 12 times (a flip-book), each with the wave a little further
/// along, and the pages are flipped through as it swims. Each page is a
/// sprite (Core/Sprite.cs): painted once, stamped every frame. Three sizes
/// are painted, because a fish far up the stream is smaller than one near
/// the bottom (perspective), and two colourings: a dark brown trout and a
/// paler golden one.
///
/// We see the fish THROUGH the water, so it is painted dim, tinted toward
/// the water's blue, foreshortened (we look down at it at an angle) and
/// stamped a little see-through. It is stamped only on open water (the
/// stencil), so stones and banks are always in front of it.
/// </summary>
internal sealed class FishSchool
{
    private const int Poses = 12, Kinds = 2, Sizes = 3;

    private struct Fish
    {
        public float Y, Lateral, LateralTarget;   // where along the stream, and how far off the middle (-1 .. 1 of half width)
        public float Vy, Wag, WagSpeed;           // speed down the screen, the flip-book clock and its rate
        public float DartIn, DartLeft;            // seconds until the next dart, seconds of dart remaining
        public int Kind;
    }

    private readonly MeadowScenery _s;
    private readonly StreamShape _stream;
    private readonly Fish[] _fish;
    private readonly Sprite[,,] _sprites = new Sprite[Kinds, Sizes, Poses];   // [kind, size, pose]
    private readonly float _u, _yMin, _yMax;
    private readonly Random _rng;

    public FishSchool(MeadowScenery s, int count, Random rng)
    {
        _s = s; _stream = s.Stream; _u = s.U; _rng = rng;
        float len = Math.Max(10f, s.U * 0.052f);          // a near fish's length; never under 10 px, so the preview box still shows one
        for (int k = 0; k < Kinds; k++)
            for (int z = 0; z < Sizes; z++)
                for (int p = 0; p < Poses; p++)
                    _sprites[k, z, p] = PaintTrout(k, p, len * (0.55f + 0.225f * z));

        // Fish live in the nearer stretch, where the water is wide enough to show them.
        _yMin = _stream.Top + (_stream.Bottom - _stream.Top) * 0.42f;
        _yMax = Math.Min(s.Height - _u * 0.03f, _stream.Bottom - _u * 0.05f);
        _fish = new Fish[Math.Max(0, count)];
        for (int i = 0; i < _fish.Length; i++)
        {
            float lat = ((float)rng.NextDouble() * 2 - 1) * 0.55f;
            _fish[i] = new Fish
            {
                Y = _yMin + (_yMax - _yMin) * (i + 0.5f + 0.4f * ((float)rng.NextDouble() - 0.5f)) / _fish.Length,   // each in its own stretch, so they do not pile up
                Lateral = lat, LateralTarget = lat,
                Vy = _u * 0.012f,
                Wag = (float)rng.NextDouble() * MathF.Tau, WagSpeed = 5f + 2f * (float)rng.NextDouble(),
                DartIn = 1f + 5f * (float)rng.NextDouble(),
                Kind = i % Kinds,
            };
        }
    }

    public void Update(float dt)
    {
        for (int i = 0; i < _fish.Length; i++)
        {
            ref Fish f = ref _fish[i];
            f.DartIn -= dt;
            if (f.DartIn <= 0)
            {
                // A dart: a burst upstream, then back to drifting.
                f.Vy = -_u * (0.08f + 0.05f * (float)_rng.NextDouble());
                f.DartLeft = 0.4f + 0.5f * (float)_rng.NextDouble();
                f.DartIn = 3f + 6f * (float)_rng.NextDouble();
                if (_rng.NextDouble() < 0.5) f.LateralTarget = ((float)_rng.NextDouble() * 2 - 1) * 0.55f;
            }
            if (f.DartLeft > 0) f.DartLeft -= dt;
            else f.Vy += (_u * 0.012f - f.Vy) * MathF.Min(1f, 2.5f * dt);     // the dart dies away into the drift
            f.Y += f.Vy * dt;
            if (f.Y > _yMax) { f.Y = _yMax; f.DartIn = 0; }                     // drifted too far back: dart now
            if (f.Y < _yMin) f.Y = _yMin;
            f.Lateral += (f.LateralTarget - f.Lateral) * MathF.Min(1f, 0.7f * dt);
            // The tail beats faster while darting. Wrapped to one turn, as every running phase is.
            f.Wag = (f.Wag + f.WagSpeed * (f.DartLeft > 0 ? 2.6f : 1f) * dt) % MathF.Tau;
        }
    }

    public void Draw(FrameBuffer fb)
    {
        foreach (ref readonly Fish f in _fish.AsSpan())
        {
            float x = _stream.CenterX(f.Y) + f.Lateral * _stream.HalfWidth(f.Y);
            float p = _stream.P(f.Y);
            int size = p < 0.62f ? 0 : p < 0.82f ? 1 : 2;
            int pose = (int)(f.Wag / MathF.Tau * Poses) % Poses;
            _sprites[f.Kind, size, pose].DrawCentered(fb, MathF.Round(x), MathF.Round(f.Y), 0.78f, _s.OpenWater);
        }
    }

    /// <summary>Looks at the fish through dusk water: pulls a colour toward the water's blue-grey.</summary>
    private static Color Water(int r, int g, int b, int a = 255) =>
        Color.FromArgb(a, (int)(r + (70 - r) * 0.30f), (int)(g + (84 - g) * 0.30f), (int)(b + (118 - b) * 0.30f));

    /// <summary>
    /// One page of the flip-book: a trout facing UP the screen, body wave at
    /// "pose" of 12. It is drawn facing right (nose at the right) along a
    /// spine, then the whole drawing is turned a quarter turn to face up and
    /// squashed along its length for the downward viewing angle.
    /// </summary>
    private static Sprite PaintTrout(int kind, int pose, float L)
    {
        int pad = (int)(L * 0.1f) + 2;
        int size = (int)MathF.Ceiling(L) + 2 * pad;
        float phase = MathF.Tau * pose / Poses;

        // A point on the spine: s = 0 at the nose, 1 at the tail tip. The
        // sideways swing is tiny at the head and grows toward the tail.
        PointF Spine(float s)
        {
            float amp = 0.01f + 0.075f * MathF.Pow(s, 1.7f);
            return new PointF((0.5f - s) * L, amp * L * MathF.Sin(MathF.Tau * 0.9f * s - phase));
        }
        PointF Side(float s)      // the direction sideways from the spine at s
        {
            PointF a = Spine(Math.Max(0, s - 0.01f)), b = Spine(Math.Min(1, s + 0.01f));
            float dx = b.X - a.X, dy = b.Y - a.Y, len = MathF.Sqrt(dx * dx + dy * dy);
            return new PointF(-dy / len, dx / len);
        }
        PointF Off(float s, float d) { var p = Spine(s); var n = Side(s); return new PointF(p.X + n.X * d, p.Y + n.Y * d); }
        float Half(float s)       // half the body's width at s: a slim, streamlined fish
        {
            float p = s < 0.28f ? MathF.Sqrt(Math.Max(0, 1 - MathF.Pow((0.28f - s) / 0.28f, 2)))
                                : 1 - 0.80f * MathF.Pow((s - 0.28f) / 0.46f, 1.2f);
            return 0.105f * L * Math.Max(0.03f, p);
        }

        Color back = kind == 0 ? Water(58, 66, 44) : Water(126, 104, 58);
        Color flank = kind == 0 ? Water(150, 140, 112) : Water(190, 166, 110);
        Color fin = kind == 0 ? Water(120, 110, 90, 150) : Water(170, 150, 100, 150);

        return Sprite.Paint(size, size, g =>
        {
            g.TranslateTransform(size / 2f, size / 2f);
            g.ScaleTransform(1f, 0.78f);               // (applied in the up-facing frame after the turn below: this squashes the length)
            g.RotateTransform(-90);                    // nose to the right becomes nose up

            const int n = 22;
            const float bodyEnd = 0.74f;
            var outline = new List<PointF>();
            for (int i = 0; i <= n; i++) outline.Add(Off(bodyEnd * i / n, Half(bodyEnd * i / n)));
            for (int i = n; i >= 0; i--) outline.Add(Off(bodyEnd * i / n, -Half(bodyEnd * i / n)));

            // A soft blur around the body: the water's doing.
            using (var blur = new Pen(Color.FromArgb(40, flank), Math.Max(2f, L * 0.07f)) { LineJoin = LineJoin.Round })
                g.DrawPolygon(blur, outline.ToArray());

            // The tail fin, a forked fan.
            using (var finBrush = new SolidBrush(fin))
                g.FillClosedCurve(finBrush, [
                    Off(0.70f, 0.03f * L), Off(0.86f, 0.09f * L), Off(1.00f, 0.15f * L), Off(0.93f, 0.0f),
                    Off(1.00f, -0.15f * L), Off(0.86f, -0.09f * L), Off(0.70f, -0.03f * L)], FillMode.Winding, 0.4f);
            // Pectoral fins, two small paddles behind the head.
            for (int side = -1; side <= 1; side += 2)
            {
                PointF root = Off(0.30f, side * Half(0.30f) * 0.9f);
                PointF dir = Side(0.30f);
                PointF tip = new(root.X + dir.X * side * 0.14f * L - 0.08f * L, root.Y + dir.Y * side * 0.14f * L);
                using var fb = new SolidBrush(fin);
                g.FillClosedCurve(fb, [root, tip, new PointF(root.X - 0.12f * L, root.Y)], FillMode.Winding, 0.5f);
            }

            // The body: flank colour, then the dark back down the middle, kept inside by a clip.
            using var bodyPath = new GraphicsPath();
            bodyPath.AddPolygon(outline.ToArray());
            using (var bodyBrush = new SolidBrush(flank))
                g.FillPath(bodyBrush, bodyPath);
            GraphicsState st = g.Save();
            g.SetClip(bodyPath, CombineMode.Intersect);
            using (var backPen = new Pen(back, Math.Max(1.5f, L * 0.11f)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                g.DrawLines(backPen, Enumerable.Range(0, 20).Select(i => Spine(i / 26f)).ToArray());
            // Spots along the back, and a faint pink band along the flank (a trout's lateral line).
            using (var spot = new SolidBrush(Water(28, 30, 26, 200)))
                for (int i = 0; i < 9; i++)
                {
                    PointF p = Off(0.12f + 0.06f * i, ((i % 2 == 0) ? 1 : -1) * Half(0.12f + 0.06f * i) * 0.4f);
                    float r = Math.Max(0.6f, L * 0.016f);
                    g.FillEllipse(spot, p.X - r, p.Y - r, r * 2, r * 2);
                }
            if (kind == 0)
                using (var band = new Pen(Water(190, 120, 110, 110), Math.Max(1f, L * 0.03f)))
                    g.DrawLines(band, Enumerable.Range(2, 16).Select(i => Off(i / 24f, Half(i / 24f) * 0.7f)).ToArray());
            g.Restore(st);

            // A small dorsal fin standing up from the back (seen from above: a dark sliver).
            using (var dorsal = new SolidBrush(Water(40, 44, 36, 180)))
                g.FillClosedCurve(dorsal, [Off(0.36f, 0), Off(0.44f, 0.03f * L), Off(0.52f, 0)], FillMode.Winding, 0.3f);

            // Eyes.
            using var eye = new SolidBrush(Color.FromArgb(220, 20, 18, 24));
            float er = Math.Max(0.8f, L * 0.02f);
            for (int side = -1; side <= 1; side += 2)
            {
                PointF e = Off(0.11f, side * Half(0.11f) * 0.6f);
                g.FillEllipse(eye, e.X - er, e.Y - er, 2 * er, 2 * er);
            }
        });
    }
}
