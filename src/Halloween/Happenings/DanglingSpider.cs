using System.Drawing.Drawing2D;
using Peckworks.Screensavers.Core;

namespace Halloween.Happenings;

/// <summary>
/// A spider lowers itself on a silk thread from one of the bare branches in
/// the top corners, bounces a little at the bottom, hangs there swinging
/// like a pendulum while its legs wiggle, then scrambles back up the thread
/// and vanishes at the branch.
///
/// FEYNMAN VERSION: a pendulum is a weight on a string. Push it and it swings
/// back and forth, a little less each time, until it hangs still. The "pivot"
/// is the point the string is tied to (here: the branch). To swing the
/// whole thread we do not move the spider and the thread separately. We
/// work out one number, the swing angle, and then place the spider at that
/// angle from the pivot, at the distance of the thread's length. The thread
/// is just a line from the pivot to the spider.
///
/// The timeline (seconds):
///   0.0 to 0.5   the spider fades in at the branch
///   0.5 to 3.2   it drops, easing out (fast at first, slowing to a stop)
///   3.2 onward   a small bounce on the thread, and the swing begins and dies down
///   9.0 to 11.2  it climbs back up, faster than it came down
///  11.2 to 12.0  it fades out, at the branch, where nobody sees it go
///
/// Like every happening, Draw works from t alone: the swing is a sine wave
/// times a falling curve, and the bounce is the same idea, so no frame needs
/// to remember the one before. It hangs against the sky, so no stencil.
/// </summary>
internal sealed class DanglingSpider : Happening
{
    private const int Poses = 16;
    private const float DropStart = 0.5f, DropEnd = 3.2f, ClimbStart = 9.0f, ClimbEnd = 11.2f;
    private static readonly Color Silk = Color.FromArgb(225, 228, 242);

    private readonly HalloweenScenery _s;
    private readonly Sprite[] _legs = new Sprite[Poses];   // the flip-book: legs at a different point of their wiggle each page
    private readonly float _bodyHalf;                      // from the thread's end down to the middle of the body
    private readonly float _thick;
    private PointF _anchor;
    private float _depth, _flip;
    private bool _ok;

    public override float Seconds => _ok ? 12f : 0.1f;      // no branch to hang from: over at once, so the director moves on

    public DanglingSpider(HalloweenScenery s)
    {
        _s = s;
        float u = s.U;
        _thick = Math.Max(1f, u * 0.0013f);

        // The body width. The whole spider with legs spans about 3.7 times
        // this, so 0.0125 U gives about 0.046 U across. Never under 4 pixels.
        float d = Math.Max(4f, u * 0.0125f);
        int size = (int)(d * 6);
        _bodyHalf = d * 0.75f;                                // thread end to body middle: the abdomen's top is 0.75 d above it
        for (int i = 0; i < Poses; i++)
        {
            float phase = i * MathF.Tau / Poses;
            _legs[i] = Sprite.Paint(size, size, g => PaintSpider(g, size / 2f, size / 2f, d, phase));
        }
    }

    /// <summary>
    /// Paints one page of the flip-book. (cx, cy) is the middle of the body;
    /// the thread ties on at the top of the round abdomen, so the abdomen is
    /// above and the small head below, the way a spider hangs.
    ///
    /// Each leg has two segments (a thigh going up and out to a "knee", a
    /// shin going down from it). "phase" is how far through the wiggle this
    /// page is; each leg is shifted a little so they ripple rather than all
    /// moving as one.
    /// </summary>
    private static void PaintSpider(Graphics g, float cx, float cy, float d, float phase)
    {
        var ink = Color.FromArgb(12, 8, 20);
        using var leg = new Pen(ink, Math.Max(1.2f, d * 0.15f)) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        for (int side = -1; side <= 1; side += 2)
            for (int i = 0; i < 4; i++)
            {
                // i = 0 reaches forward and up, i = 3 reaches back and down.
                float wiggle = 0.35f * MathF.Sin(phase + i * 1.3f + (side > 0 ? 0f : 1.9f));
                float a = (i - 1.5f) * 0.5f + wiggle;
                var root = new PointF(cx + side * d * 0.2f, cy - d * 0.15f + i * d * 0.12f);
                var knee = new PointF(root.X + side * d * 1.15f * MathF.Cos(a * 0.5f), root.Y - d * 0.55f + d * 0.9f * MathF.Sin(a));
                var foot = new PointF(knee.X + side * d * 0.8f, knee.Y + d * 1.35f + d * 0.4f * MathF.Sin(a + 1f));
                g.DrawLines(leg, [root, knee, foot]);
            }

        using var body = new SolidBrush(ink);
        g.FillEllipse(body, cx - d * 0.55f, cy - d * 0.75f, d * 1.1f, d * 1.35f);              // round abdomen
        g.FillEllipse(body, cx - d * 0.34f, cy + d * 0.38f, d * 0.68f, d * 0.62f);              // smaller head, below

        // Charm: a small orange mark on the abdomen, and two tiny orange eye glints on the head.
        using var mark = new SolidBrush(Color.FromArgb(230, 255, 120, 20));
        g.FillEllipse(mark, cx - d * 0.16f, cy - d * 0.42f, d * 0.32f, d * 0.5f);
        using var eye = new SolidBrush(Color.FromArgb(255, 255, 170, 50));
        float er = Math.Max(0.7f, d * 0.09f);
        g.FillEllipse(eye, cx - d * 0.17f - er, cy + d * 0.62f - er, er * 2, er * 2);
        g.FillEllipse(eye, cx + d * 0.17f - er, cy + d * 0.62f - er, er * 2, er * 2);
    }

    public override void Begin(Random rng)
    {
        _ok = false;
        // Candidates: branch spots on screen and in the top 45 percent.
        var spots = _s.CornerBranchSpots.Where(p => p.X > _s.Width * 0.03f && p.X < _s.Width * 0.97f && p.Y < _s.Height * 0.45f && p.Y > 0).ToList();
        if (spots.Count == 0) return;                         // no branch to hang from: draw nothing
        _anchor = spots[rng.Next(spots.Count)];
        _depth = _s.U * (0.10f + 0.12f * (float)rng.NextDouble());
        _flip = rng.Next(2) == 0 ? 1 : -1;                    // which way the first swing goes
        _ok = true;
    }

    /// <summary>How far down the thread has paid out at time t (0 = at the branch, _depth = at the bottom).</summary>
    private float Length(float t)
    {
        if (t < DropStart) return 0;
        if (t < DropEnd)
        {
            float k = (t - DropStart) / (DropEnd - DropStart);
            return _depth * (1 - (1 - k) * (1 - k) * (1 - k));              // eases out: fast, then slowing
        }
        float bounce = 0;
        float tau = t - DropEnd;                                            // seconds since arriving at the bottom
        if (t < ClimbStart)
            bounce = _depth * 0.035f * MathF.Exp(-tau * 2.2f) * MathF.Sin(tau * 9f);   // springs on the thread like a yo-yo
        if (t < ClimbStart) return _depth + bounce;
        float c = Smooth((t - ClimbStart) / (ClimbEnd - ClimbStart));       // climbing: ease in and out
        float atClimbStart = _depth;
        return atClimbStart * (1 - c);
    }

    public override void Draw(FrameBuffer fb, float t)
    {
        if (!_ok) return;
        float fade = Math.Min(Smooth(t / 0.5f), Smooth((Seconds - t) / 0.8f));

        // The swing: starts when it reaches the bottom (a bit before, so the
        // drop does not end in a dead stop), dies down. About 6 degrees to
        // start, which looks like a gentle sway, not a wild one.
        float since = Math.Max(0f, t - (DropEnd - 0.4f));
        float angle = _flip * 0.105f * MathF.Exp(-since / 3.5f) * MathF.Sin(since * 2.3f);

        float len = Length(t);
        float tx = _anchor.X + MathF.Sin(angle) * len;                      // where the thread ends: straight out from the
        float ty = _anchor.Y + MathF.Cos(angle) * len;                      // anchor at the swing angle, "len" away

        // Whole pixels, so the thread and the spider move by the same rounded amount.
        PointF end = new(MathF.Round(tx), MathF.Round(ty));
        fb.Line(_anchor, end, Silk, 0.6f * fade, _thick);

        int pose = (int)(t * 11f) % Poses;
        _legs[pose].DrawCentered(fb, end.X, end.Y + _bodyHalf, fade);
    }
}
