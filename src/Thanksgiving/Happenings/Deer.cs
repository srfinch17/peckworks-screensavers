using System.Drawing.Drawing2D;
using Peckworks.Screensavers.Core;
using Peckworks.Screensavers.Core.Sakura;

namespace Thanksgiving.Happenings;

/// <summary>
/// A doe and her fawn walk up over the far ridge against the sunset glow, stand
/// side by side, graze (heads down, a little nibbling, heads up), look up
/// together at something far away, and walk back down out of sight.
///
/// It is the Christmas reindeer's trick. Both are dark silhouettes with a thin
/// warm rim of sunset light (dark on the gold sky would be fine, but a pale rim
/// lifts them off the plum hill too). They are stamped "only where the sky is
/// open" (the OpenSky stencil). The far ridge was painted over the sky, so the
/// ridge itself hides whatever is below it: a deer is just a sprite that starts
/// sunk below the ridge line and slides up as it walks, and the stencil cuts it
/// off at the hill's edge. That is the whole of "walking up over the hill".
///
/// FEYNMAN VERSION: each animal is TWO flip-books stamped together.
///   - the BODY and legs: 16 walking pages (each foot slides back along the ground
///     while it is down, then lifts and swings forward). Page 0 is also the standing
///     pose, so a walk starts and stops with no pop. How far the animal moves is
///     tied to how many pages have gone by (a stride's worth of ground for each
///     whole walk cycle), so the feet do not skate.
///   - the NECK AND HEAD: 9 pages, the neck swinging from "up and alert" to
///     "down to the grass", with the head turned to match and the big ears
///     staying up. It is anchored on the neck's base, which rides on the body.
/// The doe is the bigger one; the fawn is the same drawing smaller, a little behind
/// her, a half second late in everything, so they never move in lockstep.
///
/// Where they stand is picked in Begin: a place where the far ridge stands clear
/// of the nearer hills, is not too steep, is away from the moon (a pale rim
/// vanishes in its glow) and away from the farmhouse and the barn. If there is no
/// such place, the showing ends at once (Seconds tiny) and the director deals again.
/// </summary>
internal sealed class Deer : Happening
{
    private const int Walk = 16, Heads = 9;
    private const float RiseFor = 2.8f, StandFor = 5.6f, DownFor = 2.8f, Lag = 0.5f;   // seconds; the fawn is Lag later in all three
    private const int Cycles = 3;                                                       // whole walk cycles on the way up, and on the way down
    private const float StepA = 0.10f;                                                  // how far a foot reaches each side of the hip, in body heights
    private const float StrideLen = 2.667f * StepA;                                     // body travel per cycle (the stance part is 3/4 of a cycle)
    private static readonly Color Dark = Color.FromArgb(36, 22, 30);
    private static readonly Color Rim = Color.FromArgb(190, 255, 196, 120);

    private sealed class Animal
    {
        public int H, BodyW, HeadS, BodyOx, BodyOy;
        public readonly Sprite[] Body = new Sprite[Walk], Head = new Sprite[Heads];
    }

    private readonly ThanksgivingScenery _s;
    private readonly Animal _doe = new(), _fawn = new();
    private bool _ok, _facingLeft;
    private float _x0, _dir;
    private float _seconds = RiseFor + StandFor + DownFor + Lag;

    public override float Seconds => _seconds;
    public override string? Claims => "ridge";

    // Where the neck begins on the body, in body heights from the body's middle and up from the ground.
    private const float NeckX = 0.27f, NeckY = 0.71f;

    public Deer(ThanksgivingScenery s)
    {
        _s = s;
        Make(_doe, Math.Max(20, (int)(s.U * 0.058f)));
        Make(_fawn, Math.Max(13, (int)(s.U * 0.038f)));
    }

    private static void Make(Animal a, int h)
    {
        a.H = h;
        a.BodyW = (int)(1.15f * h) + 4;
        a.BodyOx = a.BodyW / 2;
        a.BodyOy = (int)(0.99f * h) + 1;
        a.HeadS = (int)(1.0f * h) + 4;
        for (int i = 0; i < Walk; i++)
        {
            int pose = i;
            a.Body[i] = Sprite.Paint(a.BodyW, (int)(1.0f * h) + 4, g =>
            {
                g.TranslateTransform(a.BodyOx, a.BodyOy);
                g.ScaleTransform(h, -h);
                PaintBody(g, pose, h);
            });
        }
        for (int j = 0; j < Heads; j++)
        {
            int pg = j;
            a.Head[j] = Sprite.Paint(a.HeadS, a.HeadS, g =>
            {
                g.TranslateTransform(a.HeadS / 2f, a.HeadS / 2f);
                g.ScaleTransform(h, -h);
                PaintHead(g, pg / (float)(Heads - 1), h);
            });
        }
    }

    /// <summary>Two passes: first fat in the rim colour, then dark on top, so the rim shows only round the outside of the whole animal.</summary>
    private static void TwoPass(float h, Action<Color, float> shapes)
    {
        shapes(Rim, MathF.Max(0.025f, 1.2f / h));
        shapes(Dark, 0);
    }

    private static PointF P(float x, float y) => new(x, y);

    /// <summary>The body and legs in body heights: x forward, y up, origin at the middle of the body on the ground.</summary>
    private static void PaintBody(Graphics g, int pose, int h)
    {
        TwoPass(h, (col, grow) =>
        {
            using var brush = new SolidBrush(col);
            using var edge = new Pen(col, MathF.Max(0.001f, grow)) { LineJoin = LineJoin.Round };
            void Oval(float cx, float cy, float rx, float ry, float tilt)
            {
                var st = g.Save();
                g.TranslateTransform(cx, cy);
                g.RotateTransform(tilt);
                g.FillEllipse(brush, -rx, -ry, rx * 2, ry * 2);
                if (grow > 0) g.DrawEllipse(edge, -rx, -ry, rx * 2, ry * 2);
                g.Restore(st);
            }
            // The torso (the rump a little higher than the shoulder), the haunch, a short tail.
            Oval(0.0f, 0.60f, 0.36f, 0.15f, 4f);
            Oval(-0.20f, 0.61f, 0.19f, 0.16f, 0f);
            Oval(0.20f, 0.63f, 0.16f, 0.14f, 0f);
            Oval(-0.39f, 0.69f, 0.06f, 0.04f, -30f);
            // The chest, rising to the neck's root (the head sheet draws the neck itself).
            g.FillPolygon(brush, [P(0.14f, 0.52f), P(0.34f, 0.60f), P(NeckX + 0.05f, NeckY + 0.04f), P(0.16f, 0.72f)]);

            // Four legs: far hind, far fore, near hind, near fore. A LATERAL walk, the order every hoofed animal uses: near
            // hind lands at 0, near fore at a quarter, far hind at a half, far fore at three quarters (the first version had
            // the fore offsets swapped, which is the diagonal order a monkey walks in).
            (float hip, float offset, float knee)[] legs = [(-0.25f, 0.50f, -0.06f), (0.25f, 0.25f, 0.045f), (-0.25f, 0f, -0.06f), (0.25f, 0.75f, 0.045f)];   // the hock back, the knee forward, a little more than before so the joints read
            float legW = MathF.Max(0.085f, 2.6f / h);                                   // a deer leg is slim but not a wire: 0.058 read as stilts
            foreach (var (hipX, offset, knee) in legs)
            {
                float ph = (pose / (float)Walk + offset) % 1f;
                float dx, up = 0;
                if (ph < 0.75f) dx = StepA * (1 - 2 * ph / 0.75f);                 // standing on it: the foot slides back along the ground
                else
                {
                    float s = (ph - 0.75f) / 0.25f;
                    dx = StepA * (-1 + 2 * s);                                     // in the air: it swings forward again
                    up = 0.06f * MathF.Sin(MathF.PI * s);
                }
                PointF hipP = P(hipX, 0.50f), foot = P(hipX + dx, 0.01f + up);
                PointF kneeP = P((hipP.X + foot.X) / 2 + knee, (hipP.Y + foot.Y) / 2);
                using var pen = new Pen(col, legW + grow) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
                g.DrawLines(pen, [hipP, kneeP, foot]);
            }
        });
    }

    /// <summary>The neck and head on a sheet whose middle is the neck's root. f: 0 = head up and alert, 1 = nose down at the grass.</summary>
    private static void PaintHead(Graphics g, float f, int h)
    {
        float a = (22f + 140f * f) * MathF.PI / 180f;                              // the neck's angle from straight up, forward
        float ha = (80f + 92f * f) * MathF.PI / 180f;                              // the head's axis: forward when alert, pointing down when grazing
        float len = 0.27f + 0.10f * f;                                             // the neck stretches a little to reach the ground
        PointF root = P(0, 0);
        PointF top = P(len * MathF.Sin(a), len * MathF.Cos(a));                    // where the neck meets the head
        PointF Along(PointF from, float ang, float d) => P(from.X + d * MathF.Sin(ang), from.Y + d * MathF.Cos(ang));
        PointF headMid = Along(top, ha, 0.09f);
        float neckW = MathF.Max(0.115f, 3.2f / h);

        TwoPass(h, (col, grow) =>
        {
            using var brush = new SolidBrush(col);
            using var neck = new Pen(col, neckW + grow) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawLine(neck, grow > 0 ? P(top.X * 0.35f, top.Y * 0.35f) : root, top);    // the rim skips the root, where the neck sinks into the body
            // The head: an oval along the axis, slimmer toward the nose.
            var st = g.Save();
            g.TranslateTransform(headMid.X, headMid.Y);
            g.RotateTransform(90f - ha * 180f / MathF.PI);                         // turn the drawing's "forward" onto the head's axis (y is up here, so a positive turn is anticlockwise)
            // In this turned frame +x is the way the nose points.
            g.FillEllipse(brush, -0.11f - grow * 0.5f, -0.065f - grow * 0.5f, 0.22f + grow, 0.13f + grow);
            g.FillPolygon(brush, [P(0.04f, 0.0f), P(0.17f, 0.04f), P(0.20f, -0.01f), P(0.16f, -0.05f), P(0.04f, -0.06f)]);
            g.Restore(st);
            // Two big ears: back from the head, standing up at an angle whatever the head is doing.
            foreach (float spread in new[] { -0.28f, 0.34f })
            {
                float ea = ha - 1.95f + spread;                                    // pointing back-and-up from the head's axis
                PointF mid = Along(Along(top, ha, 0.0f), ea, 0.085f);
                var es = g.Save();
                g.TranslateTransform(mid.X, mid.Y);
                g.RotateTransform(90f - ea * 180f / MathF.PI);              // along the ear's own length
                g.FillEllipse(brush, -0.095f - grow * 0.5f, -0.040f - grow * 0.5f, 0.19f + grow, 0.08f + grow);
                g.Restore(es);
            }
        });
    }

    // ----------------------------------------------------------------- where

    /// <summary>The ridge's height at x, read straight between its corners (exactly what the painter filled).</summary>
    private static float RidgeY(PointF[] ridge, float x) => Brushwork.RidgeYAt(ridge, x);

    public override void Begin(Random rng)
    {
        _ok = false;
        _seconds = 0.2f;                                                           // "nothing to show", unless a good place turns up
        _facingLeft = rng.Next(2) == 0;
        _dir = _facingLeft ? -1 : 1;

        float u = _s.U, hd = _doe.H;
        float reach = hd * 1.7f;                                                   // the stretch of ridge the pair uses either side of the standing place
        float farmX = _s.FarmFoot.X, barnX = _s.BarnFoot.X, scarecrowX = _s.ScarecrowFoot.X;
        for (int tries = 0; tries < 120; tries++)
        {
            float x = _s.Width * (0.08f + 0.84f * (float)rng.NextDouble());
            if (MathF.Abs(x - farmX) < u * 0.20f || MathF.Abs(x - barnX) < u * 0.24f) continue;   // the buildings (and the silo) hide it
            if (MathF.Abs(x - scarecrowX) < u * 0.22f) continue;   // the scarecrow's head and scarf reach the ridge line: a doe standing behind them looked perched on its shoulder
            float footY = RidgeY(_s.FarRidge, x);

            // Far from the moon, measured to the middle of the animal.
            float mx = x - _s.Moon.At.X, my = footY - hd * 0.5f - _s.Moon.At.Y;
            if (MathF.Sqrt(mx * mx + my * my) < u * 0.16f + _s.Moon.R) continue;

            // The far ridge must stand clear above the nearer hills along the whole walk, and not be too steep under the feet.
            bool clear = true;
            for (int i = -3; i <= 3 && clear; i++)
            {
                float sx = Math.Clamp(x + reach * i / 3f, 0, _s.Width - 1);
                clear = RidgeY(_s.NearRidge, sx) - RidgeY(_s.FarRidge, sx) >= u * 0.012f;
            }
            if (!clear) continue;
            if (MathF.Abs(RidgeY(_s.FarRidge, x - hd * 0.5f) - RidgeY(_s.FarRidge, x + hd * 0.5f)) > hd * 0.22f) continue;

            _x0 = x;
            _ok = true;
            _seconds = RiseFor + StandFor + DownFor + Lag;
            return;
        }
    }

    // ------------------------------------------------------------------ draw

    /// <summary>Which head page (0 = up, 8 = down) an animal has at second u of the standing time, its own clock (the fawn's runs late).</summary>
    private static float HeadAt(float u, float graze0, float up0)
    {
        // up, then down to the grass over 0.8 s, nibbling, then up again over 0.7 s and holds.
        float down = Smooth((u - graze0) / 0.8f), back = Smooth((u - up0) / 0.7f);
        float f = down - back;
        if (f > 0.98f) f = 1f - 0.12f * (0.5f + 0.5f * MathF.Sin((u - graze0) * 6.0f));           // a nibble: the nose bobs a little
        return f;
    }

    public override void Draw(FrameBuffer fb, float t)
    {
        if (!_ok) return;
        DrawOne(fb, _doe, t, _x0);
        DrawOne(fb, _fawn, t - Lag, _x0 - _dir * _doe.H * 0.85f);
    }

    /// <summary>One animal, on its own clock "t" (the fawn's is Lag seconds behind the doe's).</summary>
    private void DrawOne(FrameBuffer fb, Animal a, float t, float x0)
    {
        if (t < 0 || t > RiseFor + StandFor + DownFor) return;
        float stride = StrideLen * a.H;

        // Where it is: walking up (eased), standing, walking down. "sink" is how far below standing height it is, 1 = wholly behind the ridge.
        float x, sink, cycles;
        if (t < RiseFor)
        {
            float p = Smooth(t / RiseFor);
            x = x0 - _dir * stride * Cycles * (1 - p); sink = 1 - p; cycles = Cycles * p;
        }
        else if (t < RiseFor + StandFor) { x = x0; sink = 0; cycles = 0; }
        else
        {
            float p = Smooth((t - RiseFor - StandFor) / DownFor);
            x = x0 + _dir * stride * Cycles * p; sink = p; cycles = Cycles * p;
        }
        bool walking = t < RiseFor || t >= RiseFor + StandFor;
        int pose = walking ? (int)(cycles * Walk) % Walk : 0;

        // The head: while walking it carries low and nods with the stride; standing, it follows the grazing timetable.
        float f;
        if (walking) f = 0.12f + 0.06f * MathF.Sin(cycles * MathF.Tau * 2);
        else
        {
            float u = t - RiseFor;
            f = HeadAt(u, 0.7f, 3.3f);
            f = Math.Max(f, 0.12f * (1f - Smooth((u - 0.2f) / 0.6f)));      // arrives with its head carried a little low, then lifts it
        }
        int hp = Math.Clamp((int)MathF.Round(f * (Heads - 1)), 0, Heads - 1);

        // A little bob while walking, a pixel at least, rounded once so the feet do not shimmer.
        float bobAmp = Math.Max(1f, _s.U * 0.0012f);
        float bob = walking && pose != 0 ? MathF.Round(bobAmp * MathF.Sin(cycles * MathF.Tau * 2)) : 0f;

        float footY = RidgeY(_s.FarRidge, x) + a.H * 0.03f;
        int top = (int)MathF.Round(footY - a.BodyOy + a.H * 1.12f * sink + bob);
        bool mirror = _facingLeft;
        int left = (int)MathF.Round(x) - (mirror ? a.BodyW - a.BodyOx : a.BodyOx);
        a.Body[pose].Draw(fb, left, top, 1f, _s.OpenSky, mirror);

        // The neck and head, anchored on the neck's root, which rides on the body.
        float rx = x + _dir * NeckX * a.H, ry = top + a.BodyOy - NeckY * a.H;
        a.Head[hp].Draw(fb, (int)MathF.Round(rx - a.HeadS / 2f), (int)MathF.Round(ry - a.HeadS / 2f), 1f, _s.OpenSky, mirror);
    }
}
