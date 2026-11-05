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
    private const float Cruise = 0.06f;         // strutting speed, in U per second: about three steps a second (0.10 was a patter of nearly five)
    private const float Ramp = 0.7f;            // seconds to slow to the stop, and again to get going
    private const float TransIn = 0.9f;         // the fan opens, the wings drop, the body puffs
    private const float Display = 3.6f;         // the full wheel, with a gobble in the middle of it
    private const float TransOut = 0.9f;
    private const float GobbleAt = 1.4f, GobbleFor = 0.7f;   // when in the display the gobble happens, and how long it takes
    private const int WalkPages = 72, TransPages = 8, QuiverPages = 4, GobblePages = 6;   // 72: at 24 the held head stepped 1.9 px a page under a body moving 1 px a frame, and buzzed
    private const float Reach = 0.11f;          // how far each foot travels in front of and behind the hip (turkey lengths)
    private const float Thigh = 0.21f;          // the hidden upper bone, hip to hock (the drumstick, inside the feathers)
    private const float Shank = 0.24f;          // the bare shank, hock to foot
    private const int Feathers = 13;            // in the tail

    private static readonly Color Bronze = Color.FromArgb(92, 58, 34);
    private static readonly Color BronzeLit = Color.FromArgb(170, 118, 62);
    private static readonly Color Dark = Color.FromArgb(58, 38, 24);
    private static readonly Color WingDark = Color.FromArgb(74, 48, 30);
    private static readonly Color WingBar = Color.FromArgb(210, 170, 110);
    private static readonly Color Buff = Color.FromArgb(222, 182, 118);
    private static readonly Color Black = Color.FromArgb(24, 16, 14);
    private static readonly Color HeadBlue = Color.FromArgb(110, 140, 196);
    private static readonly Color HeadPale = Color.FromArgb(132, 158, 206);
    private static readonly Color Wattle = Color.FromArgb(206, 40, 44);
    private static readonly Color Beak = Color.FromArgb(224, 204, 150);
    private static readonly Color LegNear = Color.FromArgb(182, 132, 122);    // a turkey's bare shank is pinkish grey, not feather brown
    private static readonly Color LegFar = Color.FromArgb(128, 90, 84);

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
        _k = Math.Max(10f, s.U * 0.075f);       // beak to tail is about a unit: on the near ground just in front of the fence, between the flock's scale behind it and the patch's (at 0.095 it dwarfed the flock)
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
        page.Draw(fb, left, top, 1f, null, false, _s.NearDepth, _s.Nearness(y));   // behind the shock, the horn and any pumpkin nearer than its feet
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
        PaintPage(_k, _w, _h, _ox, _by, mirror, phase, walk, fan, gobble, shake, 0f);

    /// <summary>
    /// Paints one page of a turkey onto a sheet "w" by "h" pixels, with the
    /// middle of the body at "ox" across and the ground line "by" down, "k"
    /// pixels to the turkey-length unit. Shared with the flock that lives in
    /// the field (TurkeyFlock.cs), which adds "peck": 0 head up, 1 the whole
    /// bird pitched forward with the beak on the ground.
    ///
    /// THE GAIT (after the animator's review): a bird's head bob is not a
    /// swing on the body. The head is HELD STILL in the world for most of
    /// each step while the body walks on under it, then THRUST forward to
    /// catch up. So over each step (half a stride) the head's place on the
    /// body slides back at exactly the body's speed for 0.6 of the step
    /// (held still in the world), then shoots forward over the other 0.4.
    /// The body dips just after each foot lands (two dips a stride, two
    /// pixels at 1080p, not the fraction of a pixel it was) and rocks a
    /// couple of degrees with each step.
    /// </summary>
    /// <summary>
    /// One page. "legs" is how far the feet are in their stride stance (-1: the same as "walk"); a standing page with legs 1
    /// and walk 0 has the feet where the walk's rest page leaves them, so a peck or a display begins without the legs
    /// snapping together. "squash" narrows the bird about its feet, for the pages of a turn (a bird coming round through
    /// the front view is a sliver).
    /// </summary>
    internal static Sprite PaintPage(float k, int w, int h, int ox, int by, bool mirror, float phase, float walk, float fan, float gobble, float shake, float peck, float legs = -1f, float squash = 1f) =>
        Sprite.Paint(w, h, g =>
        {
            // Everything below is drawn in turkey-length units: x runs along the
            // turkey (0 = the middle of the body, beak at about +0.5), y runs UP
            // from the ground. These lines turn those into pixels.
            if (mirror) { g.TranslateTransform(w, 0); g.ScaleTransform(-1, 1); }
            g.TranslateTransform(ox, by);
            g.ScaleTransform(k * squash, -k);
            Turkey_(g, phase, walk, fan, gobble, shake, peck, legs < 0 ? walk : legs);
        });

    private static PointF P(float x, float y) => new(x, y);

    private static void Turkey_(Graphics g, float phase, float walk, float fan, float gobble, float shake, float peck, float legs)
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

        float bob = -0.02f * MathF.Cos(2 * (phase - 0.3f)) * walk;     // the body dips just after each foot lands: two dips a stride
        float rock = 2f * MathF.Sin(2 * phase) * walk;                 // and rocks a couple of degrees with the steps
        float puff = 1f + 0.10f * fan;                                 // the display fluffs every feather out
        float hip = 0.30f + bob;

        // Pecking: the whole bird pitches forward about the hips (up to 28 degrees), the tail rising behind, the neck
        // reaching down so the beak is on the ground. Everything but the legs and the neck is drawn inside this turn.
        float pitch = -28f * peck + rock;
        PointF hipPivot = P(0, hip);
        PointF Pitched(PointF p)
        {
            float a = pitch * MathF.PI / 180f, ca = MathF.Cos(a), sa = MathF.Sin(a);
            float dx = p.X - hipPivot.X, dy = p.Y - hipPivot.Y;
            return P(hipPivot.X + dx * ca - dy * sa, hipPivot.Y + dx * sa + dy * ca);
        }
        GraphicsState bodyState = g.Save();
        g.TranslateTransform(hipPivot.X, hipPivot.Y);
        g.RotateTransform(pitch);
        g.TranslateTransform(-hipPivot.X, -hipPivot.Y);

        // ---- the tail, behind everything ----
        // Folded: a narrow bundle pointing back and up. Fanned: a wheel behind
        // the body. Each feather is a long wedge from the tail root, dark, with
        // a black band and a buff tip. Outer feathers first, so the middle
        // ones lie on top.
        {
            PointF root = P(-0.24f, 0.46f + bob);
            float center = (150f + (95f - 150f) * fan + shake + 12f * peck) * MathF.PI / 180f;      // the middle feather's direction (the tail lifts a little in a peck)
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

        // ---- the body (inside the pitch): a teardrop, deep at the breast and tapering to the tail root ----
        // The dark back is a crescent that follows the top edge only a quarter of the way down (the first version was a
        // dark lid on a pot); a few lighter scallops along the lower edge are the breast feathers' tips. In the display it swells.
        g.Restore(bodyState);
        // The far leg goes behind the body: legs are NOT pitched (they stand on the ground whatever the body does).
        //
        // THE LEG, A TURKEY'S AND NOT A PERSON'S. What shows under a turkey is not a thigh and a shin like ours. The
        // thigh (the drumstick) and the true knee are up inside the feathers. What hangs below the body is the bare
        // SHANK, thin and pinkish grey, and at its top the HOCK, the joint that looks like a knee bent the wrong way:
        // it is really the ankle, and it points BACKWARD. So: a hip up inside the body, an upper bone down and back
        // to the hock just under the belly (drawn thick in the body's bronze, the feathered drumstick, most of it
        // hidden behind the body), then the thin bare shank down and forward to the foot, and long toes. The first
        // version put the hip on the belly line with two equal dark bones in full view AND bent the joint forward
        // (the IK took the wrong one of its two answers): a person's leg, "people wearing turkey costumes".
        void Leg(float hx, float phi, Color shank, Color drumstick)
        {
            // Foot path: slide back along the ground (half the stride), then swing forward through the air.
            float u = ((phi % MathF.Tau) + MathF.Tau) % MathF.Tau;
            float fx, lift;
            if (u < MathF.PI) { fx = 1 - 2 * u / MathF.PI; lift = 0; }
            else { float q = (u - MathF.PI) / MathF.PI; fx = -1 + 2 * Smooth(q); lift = 0.07f * MathF.Sin(q * MathF.PI); }
            PointF hipP = P(hx + 0.06f, hip + 0.10f);                       // up inside the body, a little forward of its middle
            PointF foot = P(hx + Reach * legs * fx, lift * legs);
            // Two-bone IK with unequal bones: the law of cosines gives the angle at the hip between the straight line to
            // the foot and the upper bone; swinging the upper bone BACK of that line (minus, not plus) puts the hock behind.
            float dx = foot.X - hipP.X, dy = foot.Y - hipP.Y;
            float d = MathF.Min(MathF.Sqrt(dx * dx + dy * dy), Thigh + Shank - 0.002f);
            float cosA = Math.Clamp((Thigh * Thigh + d * d - Shank * Shank) / (2 * Thigh * d), -1f, 1f);
            float theta = MathF.Atan2(dy, dx) - MathF.Acos(cosA);
            PointF hock = P(hipP.X + Thigh * MathF.Cos(theta), hipP.Y + Thigh * MathF.Sin(theta));
            Limb(hipP, hock, 0.11f, drumstick);                              // the feathered drumstick, showing only where it bulges under the belly
            Limb(hock, foot, 0.032f, shank);                                 // the bare shank
            // Three long toes forward, fanned, and a short one back.
            foreach (float spread in (float[])[-0.045f, 0f, 0.045f])
                Limb(foot, P(foot.X + 0.13f, foot.Y + spread + 0.008f), 0.024f, shank);
            Limb(foot, P(foot.X - 0.05f, foot.Y + 0.004f), 0.02f, shank);
        }
        Leg(0.02f, phase + MathF.PI, LegFar, Dark);

        bodyState = g.Save();                                                                   // a saved state is spent once it is restored: save again for the body's turn
        g.TranslateTransform(hipPivot.X, hipPivot.Y);
        g.RotateTransform(pitch);
        g.TranslateTransform(-hipPivot.X, -hipPivot.Y);
        float by0 = bob;
        PointF B(float x, float y) => P(x * puff, by0 + 0.30f + (y - 0.30f) * puff);     // the body swells about the hips in the display
        Blob([B(0.34f, 0.44f), B(0.27f, 0.56f), B(0.04f, 0.62f), B(-0.18f, 0.58f), B(-0.30f, 0.48f), B(-0.22f, 0.32f), B(0.02f, 0.25f), B(0.24f, 0.30f)], Bronze);
        Blob([B(0.30f, 0.52f), B(0.04f, 0.62f), B(-0.20f, 0.58f), B(-0.30f, 0.48f), B(-0.20f, 0.52f), B(0.02f, 0.555f), B(0.26f, 0.49f)], Dark, 0.4f);   // the dark back, a crescent along the top
        // The breast feathers' tips: a lit scallop CUT INTO the lower edge (a lit disc straddling the edge, then the body's
        // colour over all but its rim), so the edge itself is scalloped; separate discs read as buttons.
        foreach ((float sx, float sy) in ((float, float)[])[(-0.15f, 0.30f), (-0.04f, 0.265f), (0.08f, 0.265f), (0.19f, 0.295f)])
        {
            PointF c = B(sx, sy);
            Ellipse(c.X, c.Y, 0.055f * puff, 0.04f * puff, BronzeLit);
            Ellipse(c.X, c.Y + 0.014f, 0.052f * puff, 0.038f * puff, Bronze);
        }
        // The beard: a tuft of black bristles hanging from the chest, tapering to a point.
        using (var beard = new SolidBrush(Black))
            g.FillPolygon(beard, [B(0.15f, 0.37f), B(0.20f, 0.36f), B(0.195f, 0.28f), B(0.18f, 0.24f), B(0.165f, 0.29f)]);

        // ---- the wing, a rounded wedge along the side with three pale bars that follow its curve; in the display it drops ----
        {
            float droop = 55f * fan;                                                             // degrees the wing swings down
            var st = g.Save();
            PointF sh = B(0.16f, 0.52f);
            g.TranslateTransform(sh.X, sh.Y);
            g.RotateTransform(droop);                                                             // the wing points back (negative x); with y up, a positive turn swings that tip down
            PointF[] wing = [P(0, 0.02f), P(-0.22f, 0.04f), P(-0.44f, -0.06f), P(-0.44f, -0.13f), P(-0.26f, -0.16f), P(-0.08f, -0.12f)];
            Blob(wing, WingDark, 0.4f);
            using (var bar = new Pen(WingBar, 0.016f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                for (int i = 1; i <= 3; i++)
                {
                    float bx = -0.10f - 0.10f * i;
                    g.DrawBezier(bar, P(bx + 0.02f, -0.01f - 0.015f * i), P(bx + 0.005f, -0.06f), P(bx - 0.015f, -0.09f), P(bx - 0.03f, -0.13f - 0.004f * i));
                }
            g.Restore(st);
        }
        g.Restore(bodyState);

        // ---- the neck and head, drawn in the sprite's own frame (the neck base rides the pitched body) ----
        // Walking: HELD in the world for 0.6 of each step (its place on the body slides back at the body's speed), then THRUST
        // forward over 0.4. Displaying: the head draws back over the puffed chest. Gobbling: thrown forward and down. Pecking:
        // down to the ground at the end of the reach.
        float step = ((phase % MathF.PI) + MathF.PI) % MathF.PI / MathF.PI;                     // 0 to 1 across one step (half a stride)
        float along = step < 0.4f ? Smooth(step / 0.4f) : 1f - (step - 0.4f) / 0.6f;            // 1 = thrust fully forward, 0 = at the back of its travel
        float bobX = (-0.066f + 0.132f * along) * walk;                                          // 0.132 = the body's travel during the hold (0.6 of a step of 2 x Reach)
        PointF neckBase = Pitched(B(0.28f, 0.52f));
        PointF head = P(0.42f + bobX - 0.10f * fan + 0.16f * gobble + 0.10f * peck, 0.78f - 0.02f * fan - 0.10f * gobble - 0.70f * peck);
        using (var neck = new Pen(HeadBlue, 0.05f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            g.DrawBezier(neck, neckBase, P(neckBase.X - 0.02f + 0.06f * peck, neckBase.Y + 0.12f - 0.10f * peck), P(head.X - 0.06f, head.Y - 0.10f + 0.06f * peck), head);   // a slight S
        // The wattle: a red teardrop hanging from the front of the neck, and the caruncles above it.
        PointF wat = P(neckBase.X + (head.X - neckBase.X) * 0.55f + 0.03f, neckBase.Y + (head.Y - neckBase.Y) * 0.5f);
        Blob([P(wat.X - 0.016f, wat.Y + 0.02f), P(wat.X + 0.014f, wat.Y + 0.022f), P(wat.X + 0.017f, wat.Y - 0.012f), P(wat.X, wat.Y - 0.036f), P(wat.X - 0.017f, wat.Y - 0.012f)], Wattle, 0.5f);
        Ellipse(head.X, head.Y, 0.08f, 0.066f, HeadBlue, 10f);
        Ellipse(head.X - 0.015f, head.Y + 0.02f, 0.045f, 0.035f, HeadPale, 10f);                 // the crown, a shade paler
        Ellipse(head.X + 0.02f, head.Y - 0.065f, 0.026f, 0.036f, Wattle);                        // the wattle under the chin
        using (var beak = new SolidBrush(Beak))
            g.FillPolygon(beak, [P(head.X + 0.065f, head.Y + 0.02f), P(head.X + 0.16f, head.Y - 0.01f), P(head.X + 0.065f, head.Y - 0.025f)]);
        using (var snood = new Pen(Wattle, 0.018f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            g.DrawBezier(snood, P(head.X + 0.07f, head.Y + 0.03f), P(head.X + 0.12f, head.Y + 0.035f), P(head.X + 0.13f, head.Y - 0.01f), P(head.X + 0.11f, head.Y - 0.035f));   // the snood: a small curl over the beak
        Ellipse(head.X + 0.028f, head.Y + 0.016f, 0.013f, 0.013f, Black);                        // the eye

        Leg(-0.02f, phase, LegNear, Bronze);
    }
}
