using System.Drawing.Drawing2D;
using Peckworks.Screensavers.Core;

namespace Christmas.Happenings;

/// <summary>
/// A red fox trots across the snow from off one side of the screen to off
/// the other. Half way across it stops, puts its nose down to sniff the snow
/// for a moment, lifts its head, and trots on.
///
/// FEYNMAN VERSION: a fox is a flip-book. We paint the fox many times, once
/// per "page" (each page has the legs in a slightly different place), and
/// stamp the right page each frame. The legs are not drawn by hand. Each
/// foot follows a simple rule ("slide backwards along the ground, then swing
/// forwards through the air") and a little two-bone "IK" sum works out where
/// the knee has to be so the leg reaches the foot. It is how you would bend
/// your own arm to touch a spot on a table: you pick the spot, your elbow
/// goes wherever it has to.
///
/// A trot moves the legs in diagonal pairs: front-left with back-right, then
/// the other two. 24 pages make one full stride.
///
/// The stop: the fox slows (Smooth ease), arrives at the middle exactly at
/// the page where all four feet are on the ground, then "transition" pages
/// gather the legs and lower the head (6 pages), a few "sniff" pages bob the
/// nose, and the same transition pages play backwards as it sets off again.
/// To make "exactly at that page" true, Begin-free maths in the constructor
/// picks a stride length that fits a whole number of strides into the walk
/// up to the stop.
///
/// Each page is painted twice, once facing right and once as its mirror
/// image (facing left), so a fox can come from either side. Pages are drawn
/// in two passes: first every shape with a fat pale grey-blue pen (the edge,
/// so the white chest and tail tip do not vanish into white snow), then all
/// the colors on top, so the edge shows only around the outside.
///
/// It is in front of the trees and the cabin, so there is no stencil.
/// </summary>
internal sealed class Fox : Happening
{
    // How long the whole showing lasts. NOT a fixed number: the fox trots
    // at a fixed SPEED (a fraction of U per second), so a wide screen takes
    // it longer to cross. A fixed time made it twice as fast, legs a blur,
    // on a very wide screen.
    private readonly float _total;
    private const float Cruise = 0.17f;         // trotting speed, in U per second
    private const float Ramp = 0.6f;            // seconds to slow to the stop, and again to get going
    private const float TransIn = 0.4f;         // legs gather and head goes down
    private const float Sniff = 1.5f;           // nose in the snow
    private const float TransOut = 0.4f;
    private const int WalkPages = 24, TransPages = 6, SniffPages = 4;
    private const float Reach = 0.17f;          // how far each foot travels in front of and behind its hip (fox lengths)
    private const float Legs = 0.215f;           // each leg bone

    private static readonly Color Coat = Color.FromArgb(210, 100, 40);
    private static readonly Color CoatFar = Color.FromArgb(168, 74, 30);
    private static readonly Color CoatNear = Color.FromArgb(222, 112, 48);
    private static readonly Color Cream = Color.FromArgb(252, 248, 240);
    private static readonly Color Sock = Color.FromArgb(34, 24, 22);
    private static readonly Color Edge = Color.FromArgb(150, 168, 200);   // soft grey-blue edge, so white parts show on snow

    private readonly ChristmasScenery _s;
    private readonly float _k;                  // pixels in one "fox length" unit, all the drawing below is in those units
    private readonly int _w, _h, _ox, _by;      // sprite sheet size, where the fox's origin and ground line are in it
    private readonly Sprite[][] _walk = new Sprite[2][];   // [0] faces right, [1] faces left
    private readonly Sprite[][] _trans = new Sprite[2][];
    private readonly Sprite[][] _sniff = new Sprite[2][];

    // The timetable, worked out once from the screen size.
    private readonly float _v0;                 // cruising speed, pixels per second
    private readonly float _a;                  // when the slowing starts
    private readonly float _stride;             // distance for one full stride, pixels
    private readonly float _margin;             // how far off screen it starts and ends

    private int _dir = 1;                       // 1 = heads right, -1 = heads left

    public override float Seconds => _total;
    public override int Layer => 1;                        // in front of the scenery, Santa and the lights (see ChristmasScene.Render)
    public override string? Claims => "snow";

    public Fox(ChristmasScenery s)
    {
        _s = s;
        _k = Math.Max(8f, s.U * 0.087f);        // body plus head is about 0.92 of a unit, so about 0.08 U long
        _w = (int)(1.7f * _k) + 4;
        _ox = (int)(0.95f * _k) + 2;
        _h = (int)(0.95f * _k) + 8;
        _by = _h - 5;                           // the ground line, a few pixels above the bottom for the edge

        // ---- The timetable ----
        // The fox moves at full speed v0 except while slowing, stopped, or
        // getting going. The stopped stretch ("hold") is the two transitions
        // and the sniff. Time lost to slowing: each ramp loses half its length.
        float hold = TransIn + Sniff + TransOut;
        _margin = 0.85f * _k;
        float distance = s.Width + 2 * _margin;
        _v0 = Cruise * s.U;
        _total = distance / _v0 + Ramp + hold;
        _a = distance / 2 / _v0 - Ramp / 2;                     // slow so that the stop is about the middle
        float stopAt = _v0 * (_a + Ramp / 2);                   // how far it has gone when it stops
        float wanted = 4 * Reach * _k;                          // a stride that keeps feet planted
        int strides = Math.Max(1, (int)MathF.Round(stopAt / wanted));
        _stride = stopAt / strides;                             // squeezed a touch so a WHOLE number of strides fit

        // ---- Paint every page, in both directions ----
        for (int d = 0; d < 2; d++)
        {
            bool mirror = d == 1;
            _walk[d] = new Sprite[WalkPages];
            for (int i = 0; i < WalkPages; i++)
            {
                float phase = MathF.Tau * i / WalkPages;
                _walk[d][i] = Page(mirror, phase, 1f, 0f);
            }
            _trans[d] = new Sprite[TransPages];
            for (int i = 0; i < TransPages; i++)
            {
                float x = Smooth((i + 1) / (float)(TransPages + 1));   // 0 = walking, 1 = standing nose down
                _trans[d][i] = Page(mirror, 0f, 1 - x, x);
            }
            _sniff[d] = new Sprite[SniffPages];
            float[] nose = [1.0f, 1.07f, 1.0f, 0.93f];                 // the nose bobs as it sniffs
            for (int i = 0; i < SniffPages; i++)
                _sniff[d][i] = Page(mirror, 0f, 0f, nose[i]);
        }
    }

    public override void Begin(Random rng) => _dir = rng.Next(2) == 0 ? 1 : -1;

    // ---------------------------------------------------------------- timing

    /// <summary>How much of the time up to t was spent standing still (counted as full-speed seconds lost).</summary>
    private float Lost(float t)
    {
        float hold = TransIn + Sniff + TransOut;
        float downEnd = _a + Ramp, upStart = downEnd + hold;
        if (t <= _a) return 0;
        if (t < downEnd)                                          // slowing: the lost speed is the smoothstep, whose integral is x^3 - x^4/2
        {
            float x = (t - _a) / Ramp;
            return Ramp * (x * x * x - x * x * x * x / 2);
        }
        if (t < upStart) return Ramp / 2 + (t - downEnd);
        if (t < upStart + Ramp)                                   // getting going again
        {
            float x = (t - upStart) / Ramp;
            return Ramp / 2 + hold + Ramp * (x - (x * x * x - x * x * x * x / 2));
        }
        return Ramp + hold;
    }

    // ------------------------------------------------------------------ draw

    public override void Draw(FrameBuffer fb, float t)
    {
        float dist = _v0 * (t - Lost(t));                         // how far along its path, in pixels
        float start = _dir > 0 ? -_margin : _s.Width + _margin;
        float x = start + _dir * dist;
        float y = _s.WalkY(x);                                    // feet on the near snow, in front of the pines and the cabin

        int d = _dir > 0 ? 0 : 1;
        float stopStart = _a + Ramp, hold = TransIn + Sniff + TransOut;
        Sprite page;
        if (t < stopStart || t >= stopStart + hold)
        {
            // Walking (also the slowing and the getting-going: the stride is tied to the distance).
            float cycle = dist / _stride;
            page = _walk[d][(int)MathF.Round((cycle - MathF.Floor(cycle)) * WalkPages) % WalkPages];
        }
        else
        {
            float u = t - stopStart;
            if (u < TransIn)
                page = _trans[d][Math.Min(TransPages - 1, (int)(u / TransIn * TransPages))];
            else if (u < TransIn + Sniff)
                page = _sniff[d][(int)((u - TransIn) * 5) % SniffPages];     // five bobs a second
            else
                page = _trans[d][Math.Max(0, TransPages - 1 - (int)((u - TransIn - Sniff) / TransOut * TransPages))];
        }

        int left = (int)MathF.Round(x - (d == 0 ? _ox : _w - _ox));
        int top = (int)MathF.Round(y) - _by;
        page.Draw(fb, left, top);
    }

    // ----------------------------------------------------------------- paint

    /// <summary>
    /// One page of the flip-book. "phase" is where the stride is (0 to 2 pi),
    /// "walk" says how much the legs move (1 = full trot, 0 = standing), and
    /// "head" says how far the nose is down (0 = up, 1 = in the snow).
    /// </summary>
    private Sprite Page(bool mirror, float phase, float walk, float head) =>
        Sprite.Paint(_w, _h, g =>
        {
            // Everything below is drawn in fox-length units: x runs along the
            // fox (0 = the middle of the body, nose at about +0.6), y runs UP
            // from the snow. This line turns those into pixels.
            if (mirror) { g.TranslateTransform(_w, 0); g.ScaleTransform(-1, 1); }
            g.TranslateTransform(_ox, _by);
            g.ScaleTransform(_k, -_k);
            float edge = 1.4f / _k;                               // the edge pen is about 1.4 pixels each side
            for (int pass = 0; pass < 2; pass++) Fox_(g, pass, edge, phase, walk, head);
        });

    private static PointF P(float x, float y) => new(x, y);

    private void Fox_(Graphics g, int pass, float edge, float phase, float walk, float head)
    {
        using var edgePen = new Pen(Edge, edge * 2) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round };
        using var edgeBrush = new SolidBrush(Edge);

        // A blob: pass 0 draws the pale edge, pass 1 the color.
        void Blob(PointF[] pts, Color c, bool curve = true, bool outline = true)
        {
            using var path = new GraphicsPath();
            if (curve) path.AddClosedCurve(pts, 0.5f); else path.AddPolygon(pts);
            if (pass == 0) { if (outline) { g.FillPath(edgeBrush, path); g.DrawPath(edgePen, path); } }
            else { using var b = new SolidBrush(c); g.FillPath(b, path); }
        }
        // A limb: a thick straight bar with round ends.
        void Limb(PointF a, PointF b, float w, Color c)
        {
            using var pen = new Pen(pass == 0 ? Edge : c, pass == 0 ? w + edge * 2 : w) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawLine(pen, a, b);
        }
        void Ellipse(float cx, float cy, float rx, float ry, Color c, float tilt = 0)
        {
            var st = g.Save();
            g.TranslateTransform(cx, cy);
            g.RotateTransform(tilt);
            using var path = new GraphicsPath();
            path.AddEllipse(-rx, -ry, rx * 2, ry * 2);
            if (pass == 0) { g.FillPath(edgeBrush, path); g.DrawPath(edgePen, path); }
            else { using var b = new SolidBrush(c); g.FillPath(b, path); }
            g.Restore(st);
        }

        float bob = 0.016f * MathF.Sin(2 * phase + 1f) * walk;       // body rises and dips twice a stride
        float hip = 0.37f + bob;                                      // hip height above the snow

        // ---- the tail, behind everything: a leaf shape, held out and swaying with the stride ----
        {
            float ang = 0.10f + 0.13f * MathF.Sin(phase + 1.2f) * walk - 0.22f * head;   // radians above the straight-back line (y is up)
            PointF root = P(-0.29f, 0.46f + bob);
            float ca = MathF.Cos(MathF.PI - ang), sa = MathF.Sin(MathF.PI - ang);
            PointF T(float lx, float ly) => P(root.X + lx * ca - ly * sa, root.Y + lx * sa + ly * ca);
            PointF[] leaf = [T(-0.02f, 0), T(0.12f, 0.09f), T(0.30f, 0.13f), T(0.48f, 0.095f), T(0.60f, 0), T(0.48f, -0.095f), T(0.30f, -0.13f), T(0.12f, -0.09f)];
            Blob(leaf, Coat);
            if (pass == 1)
            {
                // The white tip: fill a zig-zag region, but only inside the tail (clip to it).
                var st = g.Save();
                using var clip = new GraphicsPath();
                clip.AddClosedCurve(leaf, 0.5f);
                g.SetClip(clip, CombineMode.Intersect);
                using var white = new SolidBrush(Cream);
                g.FillPolygon(white, [T(0.40f, -0.2f), T(0.35f, -0.06f), T(0.42f, 0.04f), T(0.37f, 0.2f), T(0.9f, 0.2f), T(0.9f, -0.2f)]);
                g.Restore(st);
            }
        }

        // ---- the legs: far pair first (darker), then the body, then the near pair ----
        void Leg(float hx, float phi, bool front, Color upper)
        {
            // Foot path: slide back along the snow (half the stride), then swing forward through the air.
            float u = ((phi % MathF.Tau) + MathF.Tau) % MathF.Tau;
            float fx, lift;
            if (u < MathF.PI) { fx = 1 - 2 * u / MathF.PI; lift = 0; }                       // stance: from +Reach back to -Reach
            else { float q = (u - MathF.PI) / MathF.PI; fx = -1 + 2 * Smooth(q); lift = 0.085f * MathF.Sin(q * MathF.PI); }
            PointF hipP = P(hx, hip);
            PointF foot = P(hx + Reach * walk * fx, lift * walk);
            // Two-bone IK: where does the knee go so two bones of length Legs span hip to foot?
            float dx = foot.X - hipP.X, dy = foot.Y - hipP.Y;
            float d = MathF.Min(MathF.Sqrt(dx * dx + dy * dy), 2 * Legs - 0.002f);
            float alpha = MathF.Acos(Math.Clamp(d / (2 * Legs), -1, 1));                    // equal bones: a simple isosceles triangle
            float theta = MathF.Atan2(dy, dx) + (front ? -alpha : alpha);                    // elbows point back, hind knees point forward
            PointF knee = P(hipP.X + Legs * MathF.Cos(theta), hipP.Y + Legs * MathF.Sin(theta));
            Limb(hipP, knee, front ? 0.095f : 0.115f, upper);                                 // the top bone: coat colored
            Limb(knee, foot, 0.056f, Sock);                                                  // the lower leg: a black stocking
            Ellipse(foot.X + 0.012f, foot.Y + 0.012f, 0.034f, 0.024f, Sock);                   // the paw
        }
        float frontX = 0.20f, hindX = -0.21f;
        Leg(frontX, phase + MathF.PI, true, CoatFar);
        Leg(hindX, phase, false, CoatFar);

        // ---- body ----
        Ellipse(-0.01f, 0.435f + bob, 0.33f, 0.125f, Coat, 3f);                              // the body, nose-up by a hair
        Ellipse(0.20f, 0.445f + bob, 0.15f, 0.12f, Coat);                                    // the shoulder, a heavier front
        // The neck: a thick bar from the shoulder to where the head sits.
        float hdAng = head * 55f;                                                            // degrees the head tips down
        PointF neck = P(0.27f + 0.04f * head, 0.51f + bob - 0.18f * head);
        Limb(P(0.18f, 0.47f + bob), neck, 0.15f, Coat);
        Ellipse(0.29f, 0.41f + bob, 0.075f, 0.095f, Cream, 10f);                             // the white chest

        // ---- the head: drawn in its own little frame, tipped about the neck ----
        {
            var st = g.Save();
            g.TranslateTransform(neck.X, neck.Y);
            g.RotateTransform(-hdAng);     // the y axis is flipped (up is positive), so a NEGATIVE turn tips the nose down on screen
            // Far ear, then the skull and snout, then the cream cheek, near ear, nose, eye.
            Blob([P(0.07f, 0.10f), P(0.07f, 0.26f), P(0.15f, 0.12f)], CoatFar, false);
            Blob([P(0.09f, 0.13f), P(0.215f, 0.14f), P(0.33f, 0.05f), P(0.41f, 0.02f), P(0.34f, -0.04f), P(0.18f, -0.07f), P(0.07f, -0.01f)], Coat);
            Blob([P(0.12f, -0.025f), P(0.30f, -0.015f), P(0.345f, -0.037f), P(0.18f, -0.073f), P(0.10f, -0.04f)], Cream);
            Blob([P(0.12f, 0.12f), P(0.145f, 0.29f), P(0.215f, 0.14f)], Coat, false);          // near ear
            if (pass == 1)
            {
                using var tip = new SolidBrush(Sock);
                g.FillPolygon(tip, [P(0.139f, 0.235f), P(0.145f, 0.29f), P(0.172f, 0.20f)]);   // black ear tip
                g.FillPolygon(tip, [P(0.068f, 0.22f), P(0.07f, 0.26f), P(0.098f, 0.18f)]);
            }
            Ellipse(0.40f, 0.022f, 0.022f, 0.019f, Sock);                                      // nose
            Ellipse(0.235f, 0.075f, 0.022f, 0.019f, Sock);                                     // eye
            g.Restore(st);
        }

        // ---- the near legs, in front of the body ----
        Leg(frontX, phase, true, CoatNear);
        Leg(hindX, phase + MathF.PI, false, CoatNear);
    }
}
