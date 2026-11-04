using System.Drawing.Drawing2D;
using Peckworks.Screensavers.Core;

namespace Thanksgiving.Happenings;

/// <summary>
/// A wild turkey struts across the near ground from off one side of the
/// screen to off the other, head bobbing with every step the way a turkey's
/// does. Half way across it stops, fans its tail into the full wheel, puffs
/// up, drops its wings, quivers, throws its head and gobbles, then folds the
/// fan and struts on.
///
/// FEYNMAN VERSION: a turkey is a flip-book. We paint it many times, once
/// per "page" (each page has the legs and the head in a slightly different
/// place), and stamp the right page each frame. The legs are not drawn by
/// hand: each foot follows a rule ("slide back along the ground, then swing
/// forward through the air") and a little two-bone sum works out where the
/// ankle has to be so the leg reaches the foot (a bird's "knee" is really
/// its ankle, and it points BACKWARD). 24 pages make one stride.
///
/// The stop: the turkey slows (Smooth ease), arrives at the middle exactly
/// at the page where both feet are on the ground, then "transition" pages
/// open the fan (8 pages, each with the tail feathers spread a little
/// wider), the display holds with the fan quivering (4 pages cycling) and a
/// gobble in the middle of it (6 pages of the head thrown forward and back),
/// and the transition pages play backwards as it sets off again. To make
/// "exactly at that page" true, the constructor picks a stride length that
/// fits a whole number of strides into the walk up to the stop.
///
/// Each page is painted twice, once facing right and once as its mirror
/// image (facing left), so a turkey can come from either side. It is in
/// front of the fence and the scarecrow (its feet are on WalkY), so there is
/// no stencil. Its colours are picked to read against the ochre ground: a
/// dark bronze body, a buff band on every tail feather, a blue head and a
/// red wattle.
/// </summary>
internal sealed class Turkey : Happening
{
    // How long the whole showing lasts. NOT a fixed number: the turkey walks
    // at a fixed SPEED (a fraction of U per second), so a wide screen takes
    // it longer to cross.
    private readonly float _total;
    private const float Cruise = 0.10f;         // strutting speed, in U per second
    private const float Ramp = 0.7f;            // seconds to slow to the stop, and again to get going
    private const float TransIn = 0.9f;         // the fan opens, the wings drop, the body puffs
    private const float Display = 3.6f;         // the full wheel, with a gobble in the middle of it
    private const float TransOut = 0.9f;
    private const float GobbleAt = 1.4f, GobbleFor = 0.7f;   // when in the display the gobble happens, and how long it takes
    private const int WalkPages = 24, TransPages = 8, QuiverPages = 4, GobblePages = 6;
    private const float Reach = 0.11f;          // how far each foot travels in front of and behind the hip (turkey lengths)
    private const float Legs = 0.17f;           // each leg bone
    private const int Feathers = 13;            // in the tail

    private static readonly Color Bronze = Color.FromArgb(84, 52, 32);
    private static readonly Color BronzeLit = Color.FromArgb(118, 76, 42);
    private static readonly Color Dark = Color.FromArgb(44, 28, 20);
    private static readonly Color Buff = Color.FromArgb(222, 182, 118);
    private static readonly Color Black = Color.FromArgb(24, 16, 14);
    private static readonly Color HeadBlue = Color.FromArgb(130, 140, 164);
    private static readonly Color Wattle = Color.FromArgb(206, 40, 44);
    private static readonly Color Beak = Color.FromArgb(224, 204, 150);
    private static readonly Color LegPink = Color.FromArgb(172, 136, 116);
    private static readonly Color LegFar = Color.FromArgb(132, 100, 86);

    private readonly ThanksgivingScenery _s;
    private readonly float _k;                  // pixels in one "turkey length" unit; all the drawing below is in those units
    private readonly int _w, _h, _ox, _by;      // sprite sheet size, where the turkey's origin and ground line are in it
    private readonly Sprite[][] _walk = new Sprite[2][];    // [0] faces right, [1] faces left
    private readonly Sprite[][] _trans = new Sprite[2][];
    private readonly Sprite[][] _quiver = new Sprite[2][];
    private readonly Sprite[][] _gobble = new Sprite[2][];

    // The timetable, worked out once from the screen size.
    private readonly float _v0;                 // cruising speed, pixels per second
    private readonly float _a;                  // when the slowing starts
    private readonly float _stride;             // distance for one full stride, pixels
    private readonly float _margin;             // how far off screen it starts and ends

    private int _dir = 1;                       // 1 = heads right, -1 = heads left

    public override float Seconds => _total;
    public override int Layer => 1;                        // in front of the scenery and the geese (see ThanksgivingScene.Render)
    public override string? Claims => "ground";

    public Turkey(ThanksgivingScenery s)
    {
        _s = s;
        _k = Math.Max(10f, s.U * 0.095f);       // beak to tail is about a unit, so about 0.1 U long
        _w = (int)(1.45f * _k) + 4;
        _ox = (int)(0.80f * _k) + 2;            // the origin (the middle of the body) sits right of center: the fan reaches back
        _h = (int)(1.08f * _k) + 8;
        _by = _h - 5;                           // the ground line, a few pixels above the bottom

        // ---- The timetable (the fox's: full speed except while slowing, stopped, or getting going) ----
        float hold = TransIn + Display + TransOut;
        _margin = 0.9f * _k;
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
                _walk[d][i] = Page(mirror, MathF.Tau * i / WalkPages, 1f, 0f, 0f, 0f);
            _trans[d] = new Sprite[TransPages];
            for (int i = 0; i < TransPages; i++)
                _trans[d][i] = Page(mirror, 0f, 0f, Smooth((i + 1) / (float)(TransPages + 1)), 0f, 0f);
            _quiver[d] = new Sprite[QuiverPages];
            float[] shake = [0f, 1.2f, 0f, -1.2f];                    // degrees the fan trembles by, page by page
            for (int i = 0; i < QuiverPages; i++)
                _quiver[d][i] = Page(mirror, 0f, 0f, 1f, 0f, shake[i]);
            _gobble[d] = new Sprite[GobblePages];
            float[] throwHead = [0.35f, 0.75f, 1f, 0.8f, 0.45f, 0.15f];  // how far the head is thrown, page by page
            for (int i = 0; i < GobblePages; i++)
                _gobble[d][i] = Page(mirror, 0f, 0f, 1f, throwHead[i], 0f);
        }
    }

    public override void Begin(Random rng) => _dir = rng.Next(2) == 0 ? 1 : -1;

    // ---------------------------------------------------------------- timing

    /// <summary>How much of the time up to t was spent standing still (counted as full-speed seconds lost).</summary>
    private float Lost(float t)
    {
        float hold = TransIn + Display + TransOut;
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
        float y = _s.WalkY(x);                                    // feet on the near ground, in front of the fence

        int d = _dir > 0 ? 0 : 1;
        float stopStart = _a + Ramp, hold = TransIn + Display + TransOut;
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
            else if (u < TransIn + Display)
            {
                float v = u - TransIn;
                if (v >= GobbleAt && v < GobbleAt + GobbleFor)
                    page = _gobble[d][Math.Min(GobblePages - 1, (int)((v - GobbleAt) / GobbleFor * GobblePages))];
                else
                    page = _quiver[d][(int)(v * 7) % QuiverPages];          // the fan trembles seven times a second
            }
            else
                page = _trans[d][Math.Max(0, TransPages - 1 - (int)((u - TransIn - Display) / TransOut * TransPages))];
        }

        int left = (int)MathF.Round(x - (d == 0 ? _ox : _w - _ox));
        int top = (int)MathF.Round(y) - _by;
        page.Draw(fb, left, top);
    }

    // ----------------------------------------------------------------- paint

    /// <summary>
    /// One page of the flip-book. "phase" is where the stride is (0 to 2 pi),
    /// "walk" says how much the legs and the head move (1 = strutting, 0 =
    /// standing), "fan" how far the tail is spread (0 = folded, 1 = the full
    /// wheel, with the wings dropped and the body puffed to match), "gobble"
    /// how far the head is thrown, and "shake" a tremble on the fan in degrees.
    /// </summary>
    private Sprite Page(bool mirror, float phase, float walk, float fan, float gobble, float shake) =>
        Sprite.Paint(_w, _h, g =>
        {
            // Everything below is drawn in turkey-length units: x runs along the
            // turkey (0 = the middle of the body, beak at about +0.5), y runs UP
            // from the ground. These lines turn those into pixels.
            if (mirror) { g.TranslateTransform(_w, 0); g.ScaleTransform(-1, 1); }
            g.TranslateTransform(_ox, _by);
            g.ScaleTransform(_k, -_k);
            Turkey_(g, phase, walk, fan, gobble, shake);
        });

    private static PointF P(float x, float y) => new(x, y);

    private static void Turkey_(Graphics g, float phase, float walk, float fan, float gobble, float shake)
    {
        void Blob(PointF[] pts, Color c, float tension = 0.5f)
        {
            using var path = new GraphicsPath();
            path.AddClosedCurve(pts, tension);
            using var b = new SolidBrush(c);
            g.FillPath(b, path);
        }
        void Ellipse(float cx, float cy, float rx, float ry, Color c, float tilt = 0)
        {
            var st = g.Save();
            g.TranslateTransform(cx, cy);
            g.RotateTransform(tilt);
            using var b = new SolidBrush(c);
            g.FillEllipse(b, -rx, -ry, rx * 2, ry * 2);
            g.Restore(st);
        }
        void Limb(PointF a, PointF b, float w, Color c)
        {
            using var pen = new Pen(c, w) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawLine(pen, a, b);
        }

        float bob = 0.012f * MathF.Sin(2 * phase + 1f) * walk;        // the body rises and dips twice a stride
        float puff = 1f + 0.10f * fan;                                 // the display fluffs every feather out
        float hip = 0.30f + bob;

        // ---- the tail, behind everything ----
        // Folded: a narrow bundle pointing back and up. Fanned: a wheel behind
        // the body. Each feather is a long wedge from the tail root, dark, with
        // a black band and a buff tip. Outer feathers first, so the middle
        // ones lie on top.
        {
            PointF root = P(-0.22f, 0.44f + bob);
            float center = (150f + (95f - 150f) * fan + shake) * MathF.PI / 180f;      // the middle feather's direction
            float spread = (14f + (175f - 14f) * fan) * MathF.PI / 180f;               // the whole fan's angle
            float len = 0.42f + 0.16f * fan;
            int mid = Feathers / 2;
            for (int o = mid; o >= 0; o--)
                foreach (int i in o == 0 ? [mid] : new[] { mid - o, mid + o })
                {
                    float a = center + spread * (i - mid) / (Feathers - 1);
                    float ca = MathF.Cos(a), sa = MathF.Sin(a);
                    PointF F(float along, float across) => P(root.X + along * ca - across * sa, root.Y + along * sa + across * ca);
                    float half = 0.028f + 0.030f * fan;                                    // half the feather's width at the tip
                    using (var dark = new SolidBrush(Bronze))
                        g.FillPolygon(dark, [F(0, 0), F(len * 0.75f, -half), F(len, -half * 0.9f), F(len, half * 0.9f), F(len * 0.75f, half)]);
                    using (var band = new SolidBrush(Black))
                        g.FillPolygon(band, [F(len * 0.70f, -half * 0.95f), F(len * 0.80f, -half), F(len * 0.80f, half), F(len * 0.70f, half * 0.95f)]);
                    using (var tip = new SolidBrush(Buff))
                        g.FillPolygon(tip, [F(len * 0.82f, -half), F(len, -half * 0.9f), F(len, half * 0.9f), F(len * 0.82f, half)]);
                }
        }

        // ---- the far leg, then the body, then the near leg ----
        void Leg(float hx, float phi, Color c)
        {
            // Foot path: slide back along the ground (half the stride), then swing forward through the air.
            float u = ((phi % MathF.Tau) + MathF.Tau) % MathF.Tau;
            float fx, lift;
            if (u < MathF.PI) { fx = 1 - 2 * u / MathF.PI; lift = 0; }
            else { float q = (u - MathF.PI) / MathF.PI; fx = -1 + 2 * Smooth(q); lift = 0.07f * MathF.Sin(q * MathF.PI); }
            PointF hipP = P(hx, hip);
            PointF foot = P(hx + Reach * walk * fx, lift * walk);
            // Two-bone IK: where does the ankle go so two bones of length Legs span hip to foot? A bird's ankle points backward.
            float dx = foot.X - hipP.X, dy = foot.Y - hipP.Y;
            float d = MathF.Min(MathF.Sqrt(dx * dx + dy * dy), 2 * Legs - 0.002f);
            float alpha = MathF.Acos(Math.Clamp(d / (2 * Legs), -1, 1));
            float theta = MathF.Atan2(dy, dx) + alpha;
            PointF ankle = P(hipP.X + Legs * MathF.Cos(theta), hipP.Y + Legs * MathF.Sin(theta));
            Limb(hipP, ankle, 0.045f, c);
            Limb(ankle, foot, 0.035f, c);
            // Three toes forward, fanned, and a short one back.
            foreach (float spread in (float[])[-0.05f, 0f, 0.05f])
                Limb(foot, P(foot.X + 0.09f, foot.Y + spread + 0.01f), 0.022f, c);
            Limb(foot, P(foot.X - 0.04f, foot.Y + 0.005f), 0.02f, c);
        }
        Leg(0.02f, phase + MathF.PI, LegFar);

        // The body: a plump oval, darker on top (the back) and bronze below,
        // with the chest pushed out in front. In the display it swells.
        Ellipse(0.0f, 0.42f + bob, 0.34f * puff, 0.20f * puff, Bronze, 8f);
        Ellipse(0.06f, 0.36f + bob, 0.28f * puff, 0.14f * puff, BronzeLit, 6f);               // the lit breast
        Ellipse(-0.04f, 0.52f + bob, 0.30f * puff, 0.10f * puff, Dark, 10f);                   // the dark back
        // The beard: a tuft of black bristles hanging from the chest.
        Limb(P(0.16f, 0.36f + bob), P(0.17f, 0.26f + bob), 0.03f, Black);

        // ---- the wing, along the side; in the display it drops to the ground ----
        {
            float droop = 55f * fan;                                                             // degrees the wing swings down
            var st = g.Save();
            g.TranslateTransform(0.14f, 0.50f + bob);
            g.RotateTransform(droop);                                                             // the wing points back (negative x); with y up, a positive turn swings that tip down
            PointF[] wing = [P(0, 0.04f), P(-0.20f, 0.06f), P(-0.42f, -0.02f), P(-0.46f, -0.10f), P(-0.30f, -0.14f), P(-0.08f, -0.10f)];
            Blob(wing, Dark, 0.4f);
            using (var bar = new Pen(Color.FromArgb(214, 206, 190), 0.012f))                     // the white barring
                for (int i = 1; i <= 4; i++)
                    g.DrawLine(bar, P(-0.06f - 0.09f * i, 0.05f - 0.012f * i), P(-0.04f - 0.09f * i, -0.10f - 0.004f * i));
            g.Restore(st);
        }

        // ---- the neck and head ----
        // Walking: the head thrusts forward and back once per step (twice a
        // stride), which is the turkey's whole gait to the eye. Displaying:
        // the head draws back over the puffed chest. Gobbling: it is thrown
        // forward and down.
        float bobX = 0.045f * MathF.Sin(2 * phase) * walk;
        PointF neckBase = P(0.26f, 0.50f + bob);
        PointF head = P(0.40f + bobX - 0.10f * fan + 0.16f * gobble, 0.76f - 0.02f * fan - 0.10f * gobble);
        PointF neckMid = P(neckBase.X + (head.X - neckBase.X) * 0.4f - 0.04f * fan, neckBase.Y + (head.Y - neckBase.Y) * 0.55f);
        using (var neck = new Pen(HeadBlue, 0.075f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
            g.DrawLines(neck, [neckBase, neckMid, head]);
        // The red caruncles down the front of the neck.
        Ellipse(neckMid.X + 0.03f, neckMid.Y - 0.02f, 0.035f, 0.05f, Wattle);
        Ellipse(head.X, head.Y, 0.075f, 0.062f, HeadBlue, 10f);
        Ellipse(head.X + 0.02f, head.Y - 0.06f, 0.028f, 0.04f, Wattle);                          // the wattle under the chin
        using (var beak = new SolidBrush(Beak))
            g.FillPolygon(beak, [P(head.X + 0.06f, head.Y + 0.02f), P(head.X + 0.15f, head.Y - 0.01f), P(head.X + 0.06f, head.Y - 0.025f)]);
        Limb(P(head.X + 0.06f, head.Y + 0.025f), P(head.X + 0.10f, head.Y - 0.03f), 0.018f, Wattle);   // the snood, drooping over the beak
        Ellipse(head.X + 0.025f, head.Y + 0.015f, 0.012f, 0.012f, Black);                         // the eye

        Leg(-0.02f, phase, LegPink);
    }
}
