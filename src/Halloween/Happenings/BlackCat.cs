using System.Drawing.Drawing2D;
using Peckworks.Screensavers.Core;

namespace Halloween.Happenings;

/// <summary>
/// A black cat trots along the graveyard hill from one side of the screen to
/// the other. Near the middle it stops, turns its head to look straight out
/// at you with two glowing yellow-green eyes, blinks once, slowly, and
/// carries on.
///
/// HOW THE CAT IS DRAWN. Every picture of the cat is built from simple
/// shapes: an oval body, a thick neck line, a round head with two pointed
/// ears, a curvy tail, and four legs. Each leg is two thick line pieces
/// (a thigh and a shin) joined at a knee. We choose where the FOOT is, and a
/// little bit of triangle geometry ("two-bone IK", see Knee) works out where
/// the knee must be so the two pieces reach from the hip to that foot. It is
/// how you would pose a puppet: say where the hand goes, and the elbow
/// follows.
///
/// HOW A STEP WORKS. A trot is a cycle. In the first half of the cycle a
/// foot is on the ground and slides backward under the cat (the cat is
/// moving forward past it). In the second half the foot lifts, swings
/// forward in an arc, and plants again. Diagonal legs (front left and back
/// right) go together, the other pair half a cycle later. We paint 24 pages
/// of that cycle, a flip-book.
///
/// WHICH PAGE TO SHOW. Not the clock: the DISTANCE travelled. One full cycle
/// is one stride length of ground, so the page is (distance / stride) mod 1.
/// That keeps the feet from skating, and when the cat slows to a stop the
/// legs slow with it, for free.
///
/// Coordinates while painting are in "cat units": 1 unit is the shoulder
/// height, x forward, y up from the ground. Pictures face right; a second
/// set is painted mirrored for a cat walking left.
///
/// No stencil: the cat is on the graveyard hill, in front of everything in
/// the backdrop. It shows up because the hill's top edge (where it walks)
/// is lit by the pale mist behind it.
/// </summary>
internal sealed class BlackCat : Happening
{
    private const int Poses = 24;
    private const float Stance = 0.55f;          // how much of each step the foot is on the ground
    private const float Reach = 0.42f;           // how far (in cat units) a foot slides either side of its hip
    private const float StrideLen = 2 * Reach / Stance;  // ground covered by one full cycle, in cat units
    private const float Slow = 1f, Pause = 4f;   // seconds to slow to a stop, and to stand there (the same ramp, 1 s, is used to speed up)

    private static readonly Color Ink = Color.FromArgb(12, 8, 20);

    private readonly HalloweenScenery _s;
    private readonly float _h;                   // one cat unit in pixels
    private readonly int _w, _gy;                // sprite width, and the ground line's distance from the sprite's top
    private readonly Sprite[][] _walk = new Sprite[2][];   // [0] faces right, [1] faces left
    private readonly Sprite[] _body = new Sprite[2], _headSide = new Sprite[2], _headFront = new Sprite[2];   // the standing cat, in three layers
    private readonly Sprite _eyeGlow, _pupil;
    private float _dir, _startX, _endX, _stopX, _v0;

    public override float Seconds => 13f;

    public BlackCat(HalloweenScenery s)
    {
        _s = s;
        _h = Math.Max(8f, s.U * 0.045f);                                 // 0.045 U at the shoulder (so about 0.09 U nose to tail-root)
        _w = (int)(_h * 3.7f);
        _gy = (int)(_h * 1.95f);
        int canvasH = _gy + (int)(_h * 0.15f) + 2;

        for (int f = 0; f < 2; f++)
        {
            bool flip = f == 1;
            _walk[f] = new Sprite[Poses];
            for (int i = 0; i < Poses; i++)
            {
                float phase = i / (float)Poses;
                _walk[f][i] = Sprite.Paint(_w, canvasH, g => PaintCat(g, phase, Part.All, flip));
            }
            _body[f] = Sprite.Paint(_w, canvasH, g => PaintCat(g, -1, Part.Body, flip));
            _headSide[f] = Sprite.Paint(_w, canvasH, g => PaintCat(g, -1, Part.HeadSide, flip));
            _headFront[f] = Sprite.Paint(_w, canvasH, g => PaintCat(g, -1, Part.HeadFront, flip));
        }

        // The eyes of the "looking" pose: a soft yellow-green halo and a thin
        // dark slit in the middle, like a real cat's pupil.
        _eyeGlow = Sprite.Glow(Math.Max(4, (int)(_h * 0.17f)), Color.FromArgb(200, 255, 40));
        int pw = Math.Max(1, (int)(_h * 0.03f)), ph = Math.Max(3, (int)(_h * 0.14f));
        _pupil = Sprite.Paint(pw + 2, ph + 2, g =>
        {
            using var b = new SolidBrush(Color.FromArgb(12, 8, 20));
            g.FillEllipse(b, 1, 1, pw, ph);
        });
    }

    // ---------------- painting one pose ----------------

    private PointF Px(float x, float y) => new(_w / 2f + x * _h, _gy - y * _h);   // cat units to sprite pixels

    /// <summary>
    /// Where the knee goes so a thigh of length l1 and a shin of length l2
    /// reach from the hip to the foot. "bend" says which way the knee
    /// points: +1 forward (a back leg), -1 backward (a front leg's elbow).
    /// Two circles, one of radius l1 around the hip and one of radius l2
    /// around the foot, cross in two places; we take the one on the side we
    /// want. (If the foot is out of reach we pull it in to just reach.)
    /// </summary>
    private static (PointF Knee, PointF Foot) Knee(PointF hip, PointF foot, float l1, float l2, int bend)
    {
        float dx = foot.X - hip.X, dy = foot.Y - hip.Y;
        float len = Math.Max(0.01f, MathF.Sqrt(dx * dx + dy * dy));
        float d = Math.Min(len, l1 + l2 - 0.001f);
        float ux = dx / len, uy = dy / len;
        float a = (l1 * l1 - l2 * l2 + d * d) / (2 * d);                  // how far along hip->foot the knee's shadow falls
        float hgt = MathF.Sqrt(Math.Max(0, l1 * l1 - a * a));             // and how far it sticks out sideways
        float px = -uy, py = ux;
        if (px * bend < 0) { px = -px; py = -py; }
        return (new PointF(hip.X + ux * a + px * hgt, hip.Y + uy * a + py * hgt),
                new PointF(hip.X + ux * d, hip.Y + uy * d));
    }

    /// <summary>
    /// Where a foot is, relative to its hip, a fraction u (0 to 1) of the way
    /// through one step. First 55 percent: planted, sliding from front to
    /// back. Rest: lifted and swung forward in an arc.
    /// </summary>
    private static PointF Foot(float u)
    {
        u -= MathF.Floor(u);
        if (u < Stance) return new PointF(Reach * (1 - 2 * u / Stance), 0);
        float k = (u - Stance) / (1 - Stance);
        float e = Smooth(k);                                              // swing eases out of and into the ground
        return new PointF(-Reach + 2 * Reach * e, 0.17f * MathF.Sin(MathF.PI * k));
    }

    // Which part of the cat a picture holds. The standing cat is three layers (body, side head,
    // front head) so the head can turn without the old head ghosting underneath the new one.
    private enum Part { All, Body, HeadSide, HeadFront }

    private void PaintCat(Graphics g, float phase, Part part, bool flip)
    {
        if (flip)
        {
            g.TranslateTransform(_w, 0);                                  // mirror the whole picture left to right
            g.ScaleTransform(-1, 1);
        }
        bool standing = phase < 0;
        float walkPhase = standing ? 0 : phase;
        float sway = standing ? 0 : MathF.Sin(MathF.Tau * phase);         // the tail's swing, once per stride
        // The body rises and falls a little twice per stride (once for each pair of feet).
        float bob = standing ? 0 : 0.035f * MathF.Sin(MathF.Tau * 2 * phase + 0.6f);

        using var fill = new SolidBrush(Ink);
        float thigh = Math.Max(1.5f, _h * 0.15f), shin = Math.Max(1.2f, _h * 0.105f);
        using var thighPen = new Pen(Ink, thigh) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        using var shinPen = new Pen(Ink, shin) { StartCap = LineCap.Round, EndCap = LineCap.Round };

        // ---- four legs: (hip x, hip y, step offset, bend direction) ----
        // Front near and back far step together; front far and back near half a cycle later.
        (float hx, float off, int bend)[] legs =
        [
            (0.22f, 0.5f, -1),   // front far
            (-0.50f, 0.0f, 1),   // back far
            (0.40f, 0.0f, -1),   // front near
            (-0.72f, 0.5f, 1),   // back near
        ];
        // Standing: the feet neatly under the hips, a touch apart, no lift.
        float[] standX = [0.07f, -0.06f, -0.04f, 0.05f];
        bool bodyPart = part is Part.All or Part.Body;
        for (int i = 0; bodyPart && i < 4; i++)
        {
            var (hx, off, bend) = legs[i];
            var hip = new PointF(hx, 0.60f + bob);
            PointF rel = standing ? new PointF(standX[i], 0) : Foot(walkPhase + off);
            var (knee, foot) = Knee(hip, new PointF(hip.X + rel.X, rel.Y + 0.015f), 0.33f, 0.31f, bend);
            g.DrawLine(thighPen, Px(hip.X, hip.Y), Px(knee.X, knee.Y));
            g.DrawLine(shinPen, Px(knee.X, knee.Y), Px(foot.X, foot.Y));
            PointF paw = Px(foot.X + 0.05f, foot.Y);                      // a small paw, pointing forward
            g.FillEllipse(fill, paw.X - _h * 0.10f, paw.Y - _h * 0.045f, _h * 0.20f, _h * 0.09f);
        }

        // ---- the tail: held up in an S that sways with the stride ----
        using var tailPen = new Pen(Ink, Math.Max(1.5f, _h * 0.13f)) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        PointF[] tail =
        [
            Px(-0.85f, 0.80f + bob),
            Px(-1.15f, 0.88f + bob),
            Px(-1.27f + 0.10f * sway, 1.18f + bob),
            Px(-1.12f + 0.14f * sway, 1.42f + bob),
            Px(-1.20f + 0.20f * sway, 1.60f + bob),
        ];
        if (bodyPart) g.DrawCurve(tailPen, tail, 0.5f);

        // ---- body: a long oval, plus a rounder haunch at the back ----
        PointF b0 = Px(-0.94f, 0.99f + bob), b1 = Px(0.54f, 0.45f + bob);
        if (bodyPart) g.FillEllipse(fill, b0.X, b0.Y, b1.X - b0.X, b1.Y - b0.Y);
        PointF h0 = Px(-0.98f, 0.98f + bob), h1 = Px(-0.30f, 0.38f + bob);
        if (bodyPart) g.FillEllipse(fill, h0.X, h0.Y, h1.X - h0.X, h1.Y - h0.Y);

        float headBob = standing ? 0 : 0.03f * MathF.Sin(MathF.Tau * 2 * phase + 1.2f);
        if (part is Part.All or Part.HeadSide)
        {
            // ---- side view of the head: neck, round head, small snout, two ears ----
            using var neckPen = new Pen(Ink, Math.Max(2f, _h * 0.32f)) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawLine(neckPen, Px(0.30f, 0.80f + bob), Px(0.70f, 0.93f + bob + headBob));
            float cx = 0.78f, cy = 0.95f + bob + headBob, r = 0.20f;
            PointF hp = Px(cx - r, cy + r);
            g.FillEllipse(fill, hp.X, hp.Y, 2 * r * _h, 2 * r * _h);
            PointF sn = Px(cx + 0.06f, cy - 0.01f + 0.09f);
            g.FillEllipse(fill, sn.X, sn.Y, 0.24f * _h, 0.18f * _h);
            g.FillPolygon(fill, [Px(cx - 0.14f, cy + 0.10f), Px(cx - 0.11f, cy + 0.38f), Px(cx + 0.03f, cy + 0.17f)]);
            g.FillPolygon(fill, [Px(cx + 0.00f, cy + 0.17f), Px(cx + 0.14f, cy + 0.36f), Px(cx + 0.18f, cy + 0.07f)]);
        }
        else if (part == Part.HeadFront)
        {
            // ---- front view: the head has turned to face you ----
            using var neckPen = new Pen(Ink, Math.Max(2f, _h * 0.32f)) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawLine(neckPen, Px(0.30f, 0.80f), Px(0.66f, 0.92f));
            float cx = 0.70f, cy = 0.97f;
            PointF hp = Px(cx - 0.235f, cy + 0.205f);
            g.FillEllipse(fill, hp.X, hp.Y, 0.47f * _h, 0.41f * _h);      // a head seen from the front is a little wider than tall
            g.FillPolygon(fill, [Px(cx - 0.21f, cy + 0.08f), Px(cx - 0.17f, cy + 0.38f), Px(cx - 0.02f, cy + 0.18f)]);
            g.FillPolygon(fill, [Px(cx + 0.21f, cy + 0.08f), Px(cx + 0.17f, cy + 0.38f), Px(cx + 0.02f, cy + 0.18f)]);
        }
    }

    // ---------------- the journey ----------------

    public override void Begin(Random rng)
    {
        _dir = rng.Next(2) == 0 ? 1 : -1;
        float margin = _w / 2f;                                          // start and end with the whole sprite off screen
        _startX = _dir > 0 ? -margin : _s.Width + margin;
        _endX = _dir > 0 ? _s.Width + margin : -margin;
        _stopX = _s.Width * (0.40f + 0.20f * (float)rng.NextDouble());
        // The whole trip is Seconds, minus the slow-down, the stop and the speed-up.
        _v0 = Math.Abs(_endX - _startX) / (Seconds - Pause - Slow);
    }

    private float StopAt => Math.Abs(_stopX - _startX) / Math.Max(1f, _v0) + Slow / 2;   // when the cat arrives at its stopping place

    /// <summary>Distance travelled from the start at time t: cruise, ease to a halt, wait, ease away, cruise.</summary>
    private float Distance(float t)
    {
        float d1 = Math.Abs(_stopX - _startX), t1 = StopAt;
        if (t < t1 - Slow) return _v0 * t;
        if (t < t1)
        {
            float tau = t - (t1 - Slow);
            return _v0 * (t1 - Slow) + _v0 * tau - _v0 * tau * tau / (2 * Slow);
        }
        if (t < t1 + Pause) return d1;
        float a = t - (t1 + Pause);
        if (a < Slow) return d1 + _v0 * a * a / (2 * Slow);
        return d1 + _v0 * Slow / 2 + _v0 * (a - Slow);
    }

    /// <summary>The speed at time t as a fraction of cruising speed (0 standing, 1 flat out).</summary>
    private float Speed(float t)
    {
        float t1 = StopAt;
        if (t < t1 - Slow) return 1;
        if (t < t1) return (t1 - t) / Slow;
        if (t < t1 + Pause) return 0;
        return Math.Min(1f, (t - (t1 + Pause)) / Slow);
    }

    public override void Draw(FrameBuffer fb, float t)
    {
        float s = Distance(t);
        float x = _startX + _dir * s;
        int f = _dir > 0 ? 0 : 1;
        float ground = _s.Ground.YAt(x) + _s.U * 0.004f;                 // feet on the top edge of the hill
        float cycles = s / (StrideLen * _h);
        int pose = (int)((cycles - MathF.Floor(cycles)) * Poses) % Poses;

        float speed = Speed(t);
        float u = t - StopAt;                                            // seconds since the cat arrived at its stopping place
        // How "standing" the cat is: 1 when stopped. It melts from the
        // trotting pictures to the standing one over the last stretch of
        // slowing down, and back again as it sets off.
        float still = 1 - Smooth(speed / 0.35f);
        // How far the head has turned to the viewer: turns after a pause, back before the cat moves off.
        float turn = Math.Min(Smooth((u - 0.6f) / 0.55f), Smooth((Pause - 0.3f - u) / 0.55f));

        int left = (int)MathF.Round(x - _w / 2f), top = (int)MathF.Round(ground - _gy);
        if (still < 1) _walk[f][pose].Draw(fb, left, top, 1 - still);
        if (still > 0)
        {
            _body[f].Draw(fb, left, top, still);
            _headSide[f].Draw(fb, left, top, still * (1 - turn));
            _headFront[f].Draw(fb, left, top, still * turn);
        }

        // The eyes: two glowing halos with a slit pupil each, set into the turned head.
        // One slow blink about two seconds into the pause (the eyes sink away and glow back).
        float blinkX = (u - 1.75f) / 0.7f;
        float open = blinkX > 0 && blinkX < 1 ? 1 - MathF.Sin(MathF.PI * blinkX) : 1f;
        float eyes = turn * turn * open * still;
        if (eyes > 0.01f)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                float ex = x + _dir * (0.70f + side * 0.085f) * _h;
                float ey = ground - 0.99f * _h;
                _eyeGlow.DrawCentered(fb, MathF.Round(ex), MathF.Round(ey), eyes);
                _pupil.DrawCentered(fb, MathF.Round(ex), MathF.Round(ey), eyes * 0.9f);
            }
        }
    }
}
