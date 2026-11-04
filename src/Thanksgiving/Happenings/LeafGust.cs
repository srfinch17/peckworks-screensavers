using System.Drawing.Drawing2D;
using Peckworks.Screensavers.Core;

namespace Thanksgiving.Happenings;

/// <summary>
/// A sudden gust of wind whirls a swirl of autumn leaves across the whole
/// screen, from one side to the other (a random side each time), along a
/// sweeping S-shaped path, in about five seconds. Forty-odd small leaves,
/// gold through orange to crimson and brown, each spinning and tumbling.
///
/// Think of leaves caught by one invisible river of air. The river is a
/// curve across the screen (it rises, dips, rises: an S). Each leaf rode its
/// own numbers, rolled once in Begin:
///   - how far BEHIND the front of the gust it rides (its "lag"), so the
///     swirl is a loose crowd and not a line;
///   - how far above or below the river's middle it sits (its "lane");
///   - the size and speed of the little circle it whirls round;
///   - how fast it spins flat (turning like a dial) and how fast it tumbles
///     (turning edge-on and back, like a coin flipped in the air).
/// Draw works out where each leaf is from t alone.
///
/// Spin and tumble come from a flip-book: for each leaf shape and colour, 12
/// angles times 3 squashes (full face, three-quarters, nearly edge-on).
/// A leaf picks the page that matches its angle and its tumble right now.
///
/// No stencil: the gust is in front of everything but the falling leaves
/// (layer 1), like the snow gust in Christmas. Its speed is in U per second,
/// so a wide screen takes longer, and a leaf is never a blur.
/// </summary>
internal sealed class LeafGust : Happening
{
    private const int Max = 46, Angles = 16, Squashes = 3, Colours = 5, Kinds = 2;
    private const float Speed = 0.42f;                              // how fast the front of the gust moves, in U per second
    private static readonly Color[] Palette =
    [
        Color.FromArgb(238, 178, 44),   // gold
        Color.FromArgb(226, 124, 34),   // orange
        Color.FromArgb(198, 62, 38),    // red
        Color.FromArgb(160, 36, 40),    // crimson
        Color.FromArgb(150, 94, 44),    // brown
    ];
    private static readonly float[] Squash = [1f, 0.62f, 0.3f];

    private readonly ThanksgivingScenery _s;
    private readonly Sprite[] _page = new Sprite[Kinds * Colours * Angles * Squashes];
    private readonly int _size;                                     // each page is a square this many pixels on a side

    private int _n;
    private float _dir;                                             // +1 blows left to right, -1 right to left
    private float _seconds = 5.5f, _phase0;
    private readonly float[] _lag = new float[Max], _lane = new float[Max];     // pixels behind the front; pixels off the river's middle
    private readonly float[] _radius = new float[Max], _whirl = new float[Max];
    private readonly float[] _spin = new float[Max], _tumble = new float[Max], _phase = new float[Max];
    private readonly int[] _kind = new int[Max], _colour = new int[Max];

    public override float Seconds => _seconds;
    public override int Layer => 1;                                 // in front of the scenery, the geese and the glows

    public LeafGust(ThanksgivingScenery s)
    {
        _s = s;
        _size = Math.Max(8, (int)(s.U * 0.046f));
        for (int kind = 0; kind < Kinds; kind++)
            for (int c = 0; c < Colours; c++)
                for (int a = 0; a < Angles; a++)
                    for (int q = 0; q < Squashes; q++)
                        _page[Index(kind, c, a, q)] = PaintLeaf(kind, Palette[c], MathF.Tau * a / Angles, Squash[q]);
    }

    private static int Index(int kind, int c, int a, int q) => ((kind * Colours + c) * Angles + a) * Squashes + q;

    /// <summary>
    /// One page: a leaf of the given kind (0 a pointy maple, 1 a plain oval) turned "angle" round and
    /// squashed to "squash" of its width (like turning a card edge-on to the eye).
    /// </summary>
    private Sprite PaintLeaf(int kind, Color body, float angle, float squash)
    {
        int n = _size;
        return Sprite.Paint(n, n, g =>
        {
            g.TranslateTransform(n / 2f, n / 2f);
            g.RotateTransform(angle * 180f / MathF.PI);
            g.ScaleTransform(squash, 1f);
            float r = n * 0.46f;                                    // the leaf's reach from its middle
            using var fill = new SolidBrush(body);
            using var dark = new Pen(Color.FromArgb(150, Math.Max(0, body.R - 70), Math.Max(0, body.G - 55), Math.Max(0, body.B - 30)), Math.Max(1f, n * 0.05f));
            if (kind == 0)
            {
                // A maple: the outline is five lobes, as a list of points out and in around the middle.
                PointF[] pts =
                [
                    new(0f, -1f), new(0.16f, -0.55f), new(0.5f, -0.7f), new(0.36f, -0.2f), new(0.92f, -0.28f),
                    new(0.62f, 0.12f), new(0.8f, 0.5f), new(0.28f, 0.42f), new(0.05f, 0.95f),
                    new(-0.05f, 0.95f), new(-0.28f, 0.42f), new(-0.8f, 0.5f), new(-0.62f, 0.12f), new(-0.92f, -0.28f),
                    new(-0.36f, -0.2f), new(-0.5f, -0.7f), new(-0.16f, -0.55f),
                ];
                for (int i = 0; i < pts.Length; i++) pts[i] = new PointF(pts[i].X * r, pts[i].Y * r);
                g.FillPolygon(fill, pts);
            }
            else
            {
                // A plain leaf: a pointed oval.
                using var path = new GraphicsPath();
                path.AddBezier(0, -r, r * 0.9f, -r * 0.4f, r * 0.7f, r * 0.5f, 0, r);
                path.AddBezier(0, r, -r * 0.7f, r * 0.5f, -r * 0.9f, -r * 0.4f, 0, -r);
                g.FillPath(fill, path);
            }
            g.DrawLine(dark, 0, -r * 0.8f, 0, r * 0.95f);           // the centre vein
        });
    }

    public override void Begin(Random rng)
    {
        _dir = rng.Next(2) == 0 ? 1 : -1;
        _n = 40 + rng.Next(7);
        _phase0 = (float)rng.NextDouble() * MathF.Tau;             // where the S starts (up first, or down first)
        float U = _s.U;
        float lagMax = 0.5f * U;
        for (int i = 0; i < _n; i++)
        {
            _kind[i] = rng.NextDouble() < 0.7 ? 0 : 1;
            _colour[i] = rng.Next(Colours);
            _lag[i] = lagMax * (float)rng.NextDouble();
            _lane[i] = U * 0.2f * ((float)rng.NextDouble() + (float)rng.NextDouble() - 1f);   // clustered near the middle
            _radius[i] = U * (0.02f + 0.05f * (float)rng.NextDouble());
            _whirl[i] = (2.2f + 3f * (float)rng.NextDouble()) * (rng.Next(2) == 0 ? 1 : -1);
            _spin[i] = (1.5f + 3f * (float)rng.NextDouble()) * (rng.Next(2) == 0 ? 1 : -1);
            _tumble[i] = 2.5f + 3.5f * (float)rng.NextDouble();
            _phase[i] = (float)rng.NextDouble() * MathF.Tau;
        }
        // The front must travel the width plus a margin each end plus the longest lag, at Speed U per second.
        _seconds = (_s.Width + 0.2f * U + lagMax) / (Speed * U);
    }

    /// <summary>Where leaf i is at time t, and how visible it is (0 = off the screen).</summary>
    private (float X, float Y, float Vis, float Rot, float Tumble) Where(int i, float t)
    {
        float W = _s.Width, H = _s.Height, U = _s.U;
        // How far along its journey, in pixels from the entry edge. Negative: still waiting off screen.
        float front = Speed * U * t - 0.1f * U;
        float along = front - _lag[i];
        if (along < -0.1f * U || along > W + 0.1f * U) return (0, 0, 0, 0, 0);

        // Fade in over the first stretch of the screen and out over the last.
        float vis = Math.Min(Smooth((along + 0.1f * U) / (0.22f * U)), Smooth((W + 0.1f * U - along) / (0.22f * U)));

        // The S: the river's middle height as a function of how far across (a sine, one and a half waves).
        float across = Math.Clamp(along / W, -0.1f, 1.1f);
        float river = H * (0.42f + 0.16f * MathF.Sin(across * MathF.Tau * 0.75f * 1.33f + _phase0));
        // A slow ripple that travels along the swirl, and the leaf's own whirl.
        float ripple = U * 0.05f * MathF.Sin(across * 7f + t * 2.2f + _phase[i] * 0.3f);
        float whirl = _whirl[i] * t + _phase[i];
        float x = (_dir > 0 ? along : W - along) + MathF.Cos(whirl) * _radius[i];
        float y = river + _lane[i] + ripple + MathF.Sin(whirl) * _radius[i] * 0.8f;
        float rot = _spin[i] * t + _phase[i];
        float tumble = MathF.Abs(MathF.Cos(_tumble[i] * t + _phase[i] * 1.7f));   // 1 face-on, 0 edge-on
        return (x, y, vis, rot, tumble);
    }

    public override void Draw(FrameBuffer fb, float t)
    {
        for (int i = 0; i < _n; i++)
        {
            var (x, y, vis, rot, tumble) = Where(i, t);
            if (vis <= 0.01f) continue;
            // Pick the page: the angle (a whole turn split in 12) and the squash nearest to the tumble now.
            float turns = rot / MathF.Tau; turns -= MathF.Floor(turns);
            int a = (int)(turns * Angles + 0.5f) % Angles;
            int q = tumble > 0.81f ? 0 : tumble > 0.46f ? 1 : 2;
            Sprite page = _page[Index(_kind[i], _colour[i], a, q)];
            page.DrawCentered(fb, x, y, vis);
        }
    }
}
