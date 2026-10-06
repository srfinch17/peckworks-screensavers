using Peckworks.Screensavers.Core;

namespace Christmas.Happenings;

/// <summary>
/// Frost crystals grow across one corner of the screen, the way ice ferns
/// spread over a cold window pane: a few main stems reach out from the
/// corner, side branches sprout from them at about 60 degrees, and smaller
/// ones from those. When a tip finishes growing it gives a tiny glint.
///
/// The whole fern is built once in Begin as a list of short straight pieces
/// ("segments"). Each segment remembers where it starts, where it ends, and
/// the times between which it grows (a branch can only start growing when the
/// piece it sprouts from has finished). In Draw, a segment that has not begun
/// is skipped, one that is growing is drawn as far as it has got (a fraction
/// of the way from start to end), and a finished one is drawn whole.
///
/// About 8 seconds of growing, 4 of holding still, 4 of fading out. It is on
/// the "glass", in front of everything, so there is no stencil.
/// </summary>
internal sealed class FrostCrystals : Happening
{
    private const int MaxSegments = 420;
    private const float Reach = 0.30f;           // how far the stems reach from the corner, in U
    private static readonly Color Frost = Color.FromArgb(210, 235, 255);
    private static readonly Color Shade = Color.FromArgb(70, 100, 160);   // a deep blue under-line, so frost shows on pale snow too

    private struct Seg
    {
        public PointF A, B;                      // start and end, in pixels
        public float T0, T1;                     // when it starts and finishes growing, in seconds
        public int Level;                        // 0 = main stem, 1 = branch, 2 = twig
        public bool Tip;                         // true if nothing grows from its end
    }

    private readonly ChristmasScenery _s;
    private readonly Sprite _glint;
    private readonly List<Seg> _segs = [];
    private Random _rng = new();

    public override float Seconds => 16f;
    public override int Layer => 2;                        // on the glass: in front of everything, even the falling snow

    public FrostCrystals(ChristmasScenery s)
    {
        _s = s;
        _glint = Sprite.Glow(Math.Max(2, (int)(s.U * 0.008f)), Color.FromArgb(235, 245, 255));
    }

    public override void Begin(Random rng)
    {
        _rng = rng;
        _segs.Clear();
        float u = _s.U;
        // Pick a corner. (cornerX, cornerY) is the corner; (dirX, dirY) points into the screen from it.
        float dirX = rng.Next(2) == 0 ? 1 : -1, dirY = rng.Next(2) == 0 ? 1 : -1;
        var corner = new PointF(dirX > 0 ? 0 : _s.Width - 1, dirY > 0 ? 0 : _s.Height - 1);
        float diag = MathF.Atan2(dirY, dirX);                // the direction straight into the screen

        // Five main stems fanned across the quarter-circle of directions that point into the screen.
        const int stems = 5;
        for (int i = 0; i < stems; i++)
        {
            float a = diag + (i - (stems - 1) / 2f) * 0.30f + 0.08f * ((float)rng.NextDouble() - 0.5f);
            Grow(corner, a, u * Reach * (0.75f + 0.3f * (float)rng.NextDouble()), 0, 0f);
        }
    }

    /// <summary>
    /// Grows one branch from "from" in direction "angle", "length" pixels long, as short pieces. Each piece
    /// starts when the one before finished. Side branches are grown from some of the joints, and they
    /// call this same method for the smaller size (so it calls itself, down to level 2).
    /// </summary>
    private void Grow(PointF from, float angle, float length, int level, float startTime)
    {
        float u = _s.U;
        float piece = u * (level == 0 ? 0.03f : 0.02f);
        int count = Math.Max(1, (int)(length / piece));
        float speed = u * (level == 0 ? 0.045f : 0.04f);     // pixels per second
        PointF p = from;
        float time = startTime;
        float dir = angle;
        for (int i = 0; i < count && _segs.Count < MaxSegments; i++)
        {
            dir += 0.12f * ((float)_rng.NextDouble() - 0.5f);                  // a slight wobble, so it is not ruler-straight
            var q = new PointF(p.X + MathF.Cos(dir) * piece, p.Y + MathF.Sin(dir) * piece);
            float done = time + piece / speed;
            _segs.Add(new Seg { A = p, B = q, T0 = time, T1 = done, Level = level, Tip = i == count - 1 });
            if (level < 2 && i >= 1 && i < count - 1)
            {
                // From this joint, maybe grow a branch each side, leaning forward about 60 degrees.
                for (int side = -1; side <= 1; side += 2)
                    if (_rng.NextDouble() < (level == 0 ? 0.55 : 0.30))
                    {
                        float len = length * (level == 0 ? 0.42f : 0.40f) * (0.7f + 0.5f * (float)_rng.NextDouble()) * (1 - i / (float)count * 0.5f);
                        Grow(q, dir + side * 1.05f, len, level + 1, done);
                    }
            }
            p = q;
            time = done;
        }
    }

    public override void Draw(FrameBuffer fb, float t)
    {
        float u = _s.U;
        float fade = Fade(t, Seconds, 0.4f, 4f);
        float basic = Math.Max(1f, u * 0.002f);

        for (int i = 0; i < _segs.Count; i++)
        {
            Seg g = _segs[i];
            if (t <= g.T0) continue;
            float k = Math.Min(1f, (t - g.T0) / (g.T1 - g.T0));   // how much of this piece has grown
            var end = new PointF(g.A.X + (g.B.X - g.A.X) * k, g.A.Y + (g.B.Y - g.A.Y) * k);
            float thick = Math.Max(1f, basic * (g.Level == 0 ? 1.6f : g.Level == 1 ? 1.2f : 0.8f));
            fb.Line(g.A, end, Shade, 0.25f * fade, thick + 1.5f);
            fb.Line(g.A, end, Frost, 0.6f * fade, thick);

            // A glint on a tip that has just finished: bright at once, gone in 0.7 s.
            float since = t - g.T1;
            if (g.Tip && since > 0 && since < 0.7f)
                _glint.DrawCentered(fb, g.B.X, g.B.Y, (1 - since / 0.7f) * fade);
        }
    }
}
