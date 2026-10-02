using System.Drawing.Drawing2D;
using Peckworks.Screensavers.Core;
using Peckworks.Screensavers.Core.Sakura;

namespace Halloween;

/// <summary>
/// Autumn leaves drifting down over a moonlit graveyard, with bats.
///
/// The same theater as Sakura: a BACKDROP painted once (HalloweenPainter.cs)
/// and ACTORS drawn over it every frame. Three kinds of actor here:
///
///   - The LEAVES are the shared petal engine (Core/Sakura/PetalField.cs)
///     told to draw leaf shapes in gold and red instead of pink petals. They
///     fall, sway, spin and tumble exactly as petals do.
///   - The BATS are "sprites": each wing position is painted once at startup
///     onto a small see-through sheet (Core/Sprite.cs), and every frame we
///     stamp whichever sheet matches where the wings are in their flap, like
///     pages of a flip-book.
///   - The CANDLES in the jack-o'-lanterns: a soft orange glow stamped over
///     each pumpkin, brighter and dimmer on a jittery rhythm.
/// </summary>
internal sealed class HalloweenScene : IScreensaverScene
{
    /// <summary>Everything about one bat.</summary>
    private struct Bat
    {
        public float X, BaseY;        // where it is across the screen, and the height it flutters around
        public float Vx;              // sideways speed, pixels per second (negative = flying left)
        public float Phase, Freq, Amp;// its up-and-down swooping: where in the swoop, how fast, how far
        public float Flap, FlapSpeed; // where the wings are in their beat, and how fast they beat
        public int Size;              // which of the three sizes (0 = small and far, 2 = big and near)
    }

    private const int Poses = 7;      // wing positions per size, from fully down to fully up

    private readonly Random _rng = new();
    private readonly HalloweenScenery _scenery;
    private readonly PetalField _leaves;
    private readonly Sprite[,] _batSprites;   // [size, pose]
    private readonly float[] _batSpans;       // each size's half wingspan, in pixels
    private readonly Bat[] _bats;
    private readonly Sprite[] _candleGlows;   // one per pumpkin, sized to it
    private readonly float[] _candlePhases;
    private readonly int _w, _h;
    private float _time;

    public HalloweenScene(int width, int height, HalloweenSettings settings)
    {
        _w = width;
        _h = height;
        _scenery = HalloweenPainter.Paint(width, height, _rng);
        float u = Math.Min(height, width * 9f / 16f);   // the size unit: height, or less on a tall screen

        // ---- Leaves: the petal engine with a different outline and colors ----
        // Leaves are bigger than petals, so there are fewer of them.
        _leaves = new PetalField(width, height,
            PetalField.CountFor(width, height, settings.DensityPercent) * 6 / 10,
            settings.FallSpeedPercent / 100f, settings.WindPercent / 100f, settings.LeafSizePercent / 100f * 1.3f,
            _scenery.LeafSpots, _rng, sizeUnit: u)
        {
            Shape = PetalField.FlakeShape.Leaf,
            Colors = (Color.FromArgb(244, 184, 52), Color.FromArgb(170, 44, 22)),   // gold through to deep red
            Tint = (0.94f, 0.90f, 0.86f),                                           // moonlight: a touch dimmer than day
        };

        // ---- Bats: paint every size and wing position once ----
        _batSpans = [u * 0.016f, u * 0.026f, u * 0.040f];
        _batSprites = new Sprite[_batSpans.Length, Poses];
        for (int size = 0; size < _batSpans.Length; size++)
            for (int pose = 0; pose < Poses; pose++)
                _batSprites[size, pose] = PaintBat(_batSpans[size], pose / (Poses - 1f) * 2 - 1);

        _bats = new Bat[settings.BatCount];
        for (int i = 0; i < _bats.Length; i++)
        {
            // Sizes are handed out in order (small, small... big), so drawing
            // the array front to back puts near bats over far ones.
            int size = i * _batSpans.Length / Math.Max(1, _bats.Length);
            float speed = u * (0.07f + 0.10f * (float)_rng.NextDouble()) * (0.6f + 0.5f * size);   // near bats cross faster
            _bats[i] = new Bat
            {
                X = (float)_rng.NextDouble() * width,
                BaseY = height * (0.08f + 0.50f * (float)_rng.NextDouble()),
                Vx = _rng.Next(2) == 0 ? speed : -speed,
                Phase = (float)(_rng.NextDouble() * Math.PI * 2),
                Freq = 0.7f + 1.3f * (float)_rng.NextDouble(),
                Amp = u * (0.02f + 0.05f * (float)_rng.NextDouble()),
                Flap = (float)(_rng.NextDouble() * Math.PI * 2),
                FlapSpeed = 14f + 9f * (float)_rng.NextDouble(),
                Size = size,
            };
        }

        // ---- Candle glows: one soft orange light per pumpkin ----
        _candleGlows = new Sprite[_scenery.Pumpkins.Count];
        _candlePhases = new float[_candleGlows.Length];
        for (int i = 0; i < _candleGlows.Length; i++)
        {
            _candleGlows[i] = Sprite.Glow((int)(_scenery.Pumpkins[i].R * 3.2f), Color.FromArgb(255, 150, 40));
            _candlePhases[i] = (float)(_rng.NextDouble() * 20);
        }
    }

    /// <summary>
    /// Paints one bat, seen from the front, with its wings at one point in
    /// the flap. "lift" runs from -1 (wing tips all the way down) to +1 (all
    /// the way up).
    ///
    /// A wing is a shape with four corners: the shoulder, the wrist (the high
    /// point of the wing), the tip, and back to the body. The edge from the
    /// tip back to the body is scalloped, three little arcs, which is the
    /// detail that makes it read as a bat and not a bird. Only the right wing
    /// is worked out; the left is the same shape mirrored.
    /// </summary>
    private static Sprite PaintBat(float s, float lift)
    {
        int size = (int)(s * 2.3f) + 4;
        float cx = size / 2f, cy = size / 2f;
        return Sprite.Paint(size, size, g =>
        {
            using var ink = new SolidBrush(Color.FromArgb(10, 6, 16));
            foreach (int side in (int[])[1, -1])
            {
                PointF P(float ax, float ay) => new(cx + side * s * ax, cy + s * ay);
                PointF shoulder = P(0.08f, -0.06f);
                PointF wrist = P(0.50f, -0.14f - 0.34f * lift);
                PointF tip = P(1.00f, 0.04f - 0.48f * lift);
                PointF hip = P(0.07f, 0.22f);

                using var wing = new GraphicsPath();
                wing.AddLine(shoulder, wrist);
                wing.AddLine(wrist, tip);
                // Three scallops from the tip back to the body. Each arc runs
                // between two points on the tip-to-hip line and is pulled up
                // toward the wrist in the middle.
                PointF from = tip;
                for (int k = 1; k <= 3; k++)
                {
                    float t = k / 3f;
                    var to = new PointF(tip.X + (hip.X - tip.X) * t, tip.Y + (hip.Y - tip.Y) * t + (k < 3 ? s * 0.07f : 0));
                    var pull = new PointF((from.X + to.X) / 2 + (wrist.X - (from.X + to.X) / 2) * 0.3f,
                                          (from.Y + to.Y) / 2 + (wrist.Y - (from.Y + to.Y) / 2) * 0.3f);
                    wing.AddBezier(from, pull, pull, to);
                    from = to;
                }
                wing.CloseFigure();
                g.FillPath(ink, wing);

                // One ear on each side of the head.
                g.FillPolygon(ink, [P(0.02f, -0.22f), P(0.10f, -0.40f), P(0.13f, -0.18f)]);
            }
            g.FillEllipse(ink, cx - s * 0.10f, cy - s * 0.14f, s * 0.20f, s * 0.40f);   // body
            g.FillEllipse(ink, cx - s * 0.10f, cy - s * 0.30f, s * 0.20f, s * 0.20f);   // head

            if (s >= 24)   // only bats big enough to show them get eyes
            {
                using var eye = new SolidBrush(Color.FromArgb(255, 196, 60));
                float er = Math.Max(1f, s * 0.022f);
                g.FillEllipse(eye, cx - s * 0.045f - er, cy - s * 0.21f - er, er * 2, er * 2);
                g.FillEllipse(eye, cx + s * 0.045f - er, cy - s * 0.21f - er, er * 2, er * 2);
            }
        });
    }

    public void Update(double elapsedSeconds)
    {
        float dt = (float)elapsedSeconds;
        _time += dt;
        _leaves.Update(dt);

        for (int i = 0; i < _bats.Length; i++)
        {
            ref Bat b = ref _bats[i];
            b.X += b.Vx * dt;
            b.Phase += b.Freq * dt;
            b.Flap += b.FlapSpeed * dt;

            // Flown off one side: come back in on the other, at a new height,
            // so it does not look like the same bat on a loop.
            float margin = _batSpans[b.Size] * 1.5f;
            if (b.X > _w + margin || b.X < -margin)
            {
                b.X = b.X > _w ? -margin : _w + margin;
                b.BaseY = _h * (0.08f + 0.50f * (float)_rng.NextDouble());
            }
        }
    }

    public void Render(FrameBuffer fb)
    {
        // 1. The backdrop. Array.Copy is one fast block copy of every pixel.
        Array.Copy(_scenery.Pixels, fb.Pixels, fb.Pixels.Length);

        // 2. Candlelight. Two waves of different speeds added together make
        //    the brightness wander unevenly, the way a real flame does.
        for (int i = 0; i < _candleGlows.Length; i++)
        {
            float p = _candlePhases[i];
            float flicker = 0.42f + 0.16f * MathF.Sin(_time * 9f + p) + 0.10f * MathF.Sin(_time * 23f + p * 2);
            _candleGlows[i].DrawCentered(fb, _scenery.Pumpkins[i].At.X, _scenery.Pumpkins[i].At.Y, flicker);
        }

        // 3. The bats, far (small) to near (big).
        foreach (ref readonly Bat b in _bats.AsSpan())
        {
            // Two swoops of different lengths added together, so the path
            // wanders instead of tracing a tidy wave.
            float y = b.BaseY + b.Amp * MathF.Sin(b.Phase) + b.Amp * 0.45f * MathF.Sin(b.Phase * 2.7f + 1);
            float lift = MathF.Sin(b.Flap);                         // -1 = wings down ... +1 = wings up
            int pose = (int)MathF.Round((lift + 1) / 2 * (Poses - 1));
            // The body bobs against the wings: wings down pushes the bat up.
            _batSprites[b.Size, pose].DrawCentered(fb, b.X, y + lift * _batSpans[b.Size] * 0.10f);
        }

        // 4. The leaves, far to near.
        _leaves.Draw(fb);
    }

    public void Dispose() { }
}
