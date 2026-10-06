using System.Drawing.Drawing2D;
using Peckworks.Screensavers.Core;

namespace Sakura.Happenings;

/// <summary>
/// A Shiba Inu, Japan's beloved little dog, trots along the grassy hill. Half
/// way along it stops, sits down facing YOU, and gives the famous Shiba smile
/// (open mouth, pink tongue, round shining eyes) while its curled tail wags
/// and its head tips to one side. Then it gets up and trots on.
///
/// FEYNMAN VERSION: a flip-book, like the fox in Christmas.
///  - The walk is 24 pages. Each foot follows a simple rule ("slide back along
///    the ground, then swing forward through the air") and a little two-bone
///    sum ("IK") works out where the knee must be, the way your own elbow goes
///    wherever it must so your hand can touch a spot on a table. Diagonal
///    legs go together (a trot). The body rises and dips, the curled tail
///    bounces.
///  - To stop, 6 pages gather the legs under the body, then the side-on dog
///    melts into a second drawing: the dog seen from the FRONT, sitting. That
///    front drawing has 8 tail-wag pages for each of 3 head-tilt angles, so
///    the wag and the tilt can happen together.
///  - It trots at a steady speed (a fraction of U per second), so a wide
///    screen simply takes longer, and the stop is timed so the feet land on
///    the stopping page.
///
/// Where it walks: the hill runs from half way across to the right edge. The
/// left end of the hill slopes down into the lake, so on that side the dog
/// fades in or out (as if slipping into the long grass) instead of walking
/// into the water. The other end is the screen's edge. It stands on
/// RightWalkY, which is a little in front of the lone tree, so it passes in
/// front of the trunk.
/// </summary>
internal sealed class ShibaInu : Happening
{
    private const float Cruise = 0.10f;          // trotting speed, U per second
    private const float Ramp = 0.6f;             // seconds to slow down, and again to speed up
    private const float TransIn = 0.4f;          // legs gather under it
    private const float XFade = 0.3f;           // side-on dog melts into front-on dog
    private const float SitTime = 5.0f;          // sitting and smiling
    private const float TransOut = 0.4f;
    private const int WalkPages = 24, TransPages = 6, WagPages = 8, TiltPages = 3;
    private const float Reach = 0.15f;           // how far each foot travels in front of and behind its hip (dog lengths)
    private const float Bone = 0.17f;            // each leg bone

    private static readonly Color Coat = Color.FromArgb(222, 124, 54);       // red-orange, "aka" Shiba
    private static readonly Color CoatFar = Color.FromArgb(184, 96, 42);
    private static readonly Color Cream = Color.FromArgb(252, 242, 224);     // "urajiro": the cream cheeks, chest, belly, legs
    private static readonly Color CreamFar = Color.FromArgb(214, 198, 172);
    private static readonly Color Ink = Color.FromArgb(40, 26, 24);
    private static readonly Color Edge = Color.FromArgb(120, 74, 40, 24);    // a soft dark brown line round the outside

    private readonly Scenery _s;
    private readonly float _k;                   // pixels in one "dog length" unit; all the drawing is in those units
    private readonly int _w, _h, _ox, _by;       // side-on sheet: size, where the dog's middle and ground line are
    private readonly int _sw, _sh;               // front-on sheet size
    private readonly Sprite[][] _walk = new Sprite[2][];    // [0] faces right, [1] faces left
    private readonly Sprite[][] _trans = new Sprite[2][];
    private readonly Sprite[] _stand = new Sprite[2];
    private readonly Sprite[,] _sit = new Sprite[TiltPages, WagPages];

    private readonly float _total, _v0, _a, _stride, _margin;
    private readonly float _fromX, _toX;         // the two ends of its path: the hill's left end and just past the right edge

    private int _dir = 1;                        // 1 = heads right, -1 = heads left

    public override float Seconds => _total;
    public override int Layer => 1;                         // in front of the lone tree
    public override string? Claims => "righthill";

    public ShibaInu(Scenery s)
    {
        _s = s;
        _k = Math.Max(10f, s.U * 0.066f);
        _w = (int)(1.7f * _k) + 4;
        _ox = (int)(0.9f * _k) + 2;
        _h = (int)(1.0f * _k) + 12;
        _by = _h - 9;
        _sw = (int)(1.3f * _k) + 4;
        _sh = (int)(1.0f * _k) + 8;

        // ---- The timetable ----
        float hold = TransIn + XFade + SitTime + XFade + TransOut;
        _margin = 0.9f * _k;
        _fromX = s.Width * 0.62f;
        _toX = s.Width + _margin;
        float distance = _toX - _fromX;
        _v0 = Cruise * s.U;
        _total = distance / _v0 + Ramp + hold;
        _a = distance / 2 / _v0 - Ramp / 2;                      // slow so that the stop is about the middle
        float stopAt = _v0 * (_a + Ramp / 2);
        float wanted = 4 * Reach * _k;
        int strides = Math.Max(1, (int)MathF.Round(stopAt / wanted));
        _stride = stopAt / strides;

        for (int d = 0; d < 2; d++)
        {
            bool mirror = d == 1;
            _walk[d] = new Sprite[WalkPages];
            for (int i = 0; i < WalkPages; i++) _walk[d][i] = Side(mirror, MathF.Tau * i / WalkPages, 1f);
            _trans[d] = new Sprite[TransPages];
            for (int i = 0; i < TransPages; i++) _trans[d][i] = Side(mirror, 0f, 1 - Smooth((i + 1) / (float)(TransPages + 1)));
            _stand[d] = Side(mirror, 0f, 0f);
        }
        float[] tilts = [0f, 7f, 13f];
        for (int ti = 0; ti < TiltPages; ti++)
            for (int wi = 0; wi < WagPages; wi++)
                _sit[ti, wi] = Front(MathF.Tau * wi / WagPages, tilts[ti]);
    }

    public override void Begin(Random rng) => _dir = rng.Next(2) == 0 ? 1 : -1;

    // ---------------------------------------------------------------- timing

    /// <summary>How much of the time up to t was spent not moving at full speed, in full-speed seconds lost (see Christmas Fox).</summary>
    private float Lost(float t)
    {
        float hold = TransIn + XFade + SitTime + XFade + TransOut;
        float downEnd = _a + Ramp, upStart = downEnd + hold;
        if (t <= _a) return 0;
        if (t < downEnd)
        {
            float x = (t - _a) / Ramp;
            return Ramp * (x * x * x - x * x * x * x / 2);
        }
        if (t < upStart) return Ramp / 2 + (t - downEnd);
        if (t < upStart + Ramp)
        {
            float x = (t - upStart) / Ramp;
            return Ramp / 2 + hold + Ramp * (x - (x * x * x - x * x * x * x / 2));
        }
        return Ramp + hold;
    }

    // ------------------------------------------------------------------ draw

    public override void Draw(FrameBuffer fb, float t)
    {
        float dist = _v0 * (t - Lost(t));
        float from = _dir > 0 ? _fromX : _toX, to = _dir > 0 ? _toX : _fromX;
        float x = from + _dir * dist;
        float y = _s.RightWalkY(x);

        // Near the hill's left end it fades (it slips into the long grass there).
        float fadeEdge = Smooth((x - _s.Width * 0.62f) / (_s.Width * 0.06f));
        // A soft start and finish at the screen edge costs nothing: it is off screen.
        float vis = fadeEdge;
        if (vis < 0.01f) return;

        int d = _dir > 0 ? 0 : 1;
        float stopStart = _a + Ramp;
        float u = t - stopStart;
        float hold = TransIn + XFade + SitTime + XFade + TransOut;

        Sprite? side = null;
        float sideA = 1f;
        Sprite? sit = null;
        float sitA = 0f;

        if (u < 0 || u >= hold)
        {
            float cycle = dist / _stride;
            side = _walk[d][(int)MathF.Round((cycle - MathF.Floor(cycle)) * WalkPages) % WalkPages];
        }
        else if (u < TransIn)
            side = _trans[d][Math.Min(TransPages - 1, (int)(u / TransIn * TransPages))];
        else if (u >= hold - TransOut)
            side = _trans[d][Math.Max(0, TransPages - 1 - (int)((u - (hold - TransOut)) / TransOut * TransPages))];
        else
        {
            side = _stand[d];
            float v = u - TransIn;                                  // time into the sit, counting the melt-in
            float into = Smooth(v / XFade), outOf = Smooth((SitTime + XFade - v) / XFade);
            sitA = Math.Min(into, outOf);
            // the side-on dog fades out a beat after the front-on one starts, and back in as it leaves
            sideA = 1 - Math.Min(Smooth((v - 0.12f) / XFade), Smooth((SitTime + XFade - 0.12f - v) / XFade));

            // The head tilt: tip over, hold, come back. The tail wags the whole time.
            float sv = v - XFade;
            int tilt = 0;
            if (sv > 1.3f && sv < 3.4f)
            {
                float ts = sv < 1.8f ? (sv - 1.3f) / 0.5f : sv < 2.9f ? 1f : (3.4f - sv) / 0.5f;
                tilt = (int)MathF.Round(Smooth(ts) * (TiltPages - 1));
            }
            int wag = sv < 0.35f ? 0 : (int)(sv * 3.2f * WagPages) % WagPages;
            sit = _sit[tilt, wag];
        }

        int ground = (int)MathF.Round(y);
        if (side != null && sideA > 0.01f)
        {
            int left = (int)MathF.Round(x - (d == 0 ? _ox : _w - _ox));
            side.Draw(fb, left, ground - _by, sideA * vis);
        }
        if (sit != null && sitA > 0.01f)
            sit.Draw(fb, (int)MathF.Round(x - _sw / 2f), ground - (_sh - 5), sitA * vis);
    }

    // ----------------------------------------------------------------- paint: shared

    private static PointF P(float x, float y) => new(x, y);

    /// <summary>
    /// The drawing helpers both views share. Everything is drawn twice (pass 0, then pass 1):
    /// first every shape with a fat soft brown outline pen, then all the colours on top, so the
    /// line shows only round the OUTSIDE of the dog and the orange meets the cream without a line.
    /// </summary>
    private sealed class Ink_
    {
        private readonly Graphics _g;
        private readonly int _pass;
        private readonly float _edge;
        private readonly Pen _edgePen;
        private readonly SolidBrush _edgeBrush = new(Edge);
        public Ink_(Graphics g, int pass, float edge)
        {
            _g = g; _pass = pass; _edge = edge;
            _edgePen = new Pen(Edge, edge * 2) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round };
        }
        public bool Colours => _pass == 1;
        public void Blob(PointF[] pts, Color c, bool curve = true, bool outline = true)
        {
            using var path = new GraphicsPath();
            if (curve) path.AddClosedCurve(pts, 0.5f); else path.AddPolygon(pts);
            if (_pass == 0) { if (outline) { _g.FillPath(_edgeBrush, path); _g.DrawPath(_edgePen, path); } }
            else { using var b = new SolidBrush(c); _g.FillPath(b, path); }
        }
        public void Limb(PointF a, PointF b, float w, Color c)
        {
            using var pen = new Pen(_pass == 0 ? Edge : c, _pass == 0 ? w + _edge * 2 : w) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            _g.DrawLine(pen, a, b);
        }
        public void Ellipse(float cx, float cy, float rx, float ry, Color c, float tilt = 0, bool outline = true)
        {
            var st = _g.Save();
            _g.TranslateTransform(cx, cy);
            _g.RotateTransform(tilt);
            using var path = new GraphicsPath();
            path.AddEllipse(-rx, -ry, rx * 2, ry * 2);
            if (_pass == 0) { if (outline) { _g.FillPath(_edgeBrush, path); _g.DrawPath(_edgePen, path); } }
            else { using var b = new SolidBrush(c); _g.FillPath(b, path); }
            _g.Restore(st);
        }
        /// <summary>Colour-pass-only detail (eyes, nose, mouth): no outline.</summary>
        public void Detail(float cx, float cy, float rx, float ry, Color c, float tilt = 0)
        {
            if (_pass == 0) return;
            Ellipse(cx, cy, rx, ry, c, tilt);
        }
    }

    // ----------------------------------------------------------------- paint: side view

    private Sprite Side(bool mirror, float phase, float walk) =>
        Sprite.Paint(_w, _h, g =>
        {
            if (mirror) { g.TranslateTransform(_w, 0); g.ScaleTransform(-1, 1); }
            g.TranslateTransform(_ox, _by);
            g.ScaleTransform(_k, -_k);                      // y runs UP from the ground
            float edge = 1.3f / _k;
            using (var shadow = new SolidBrush(Color.FromArgb(70, 20, 30, 14)))
                g.FillEllipse(shadow, -0.40f, -0.035f, 0.92f, 0.08f);
            for (int pass = 0; pass < 2; pass++) SideDog(new Ink_(g, pass, edge), g, phase, walk);
        });

    private static void SideDog(Ink_ ink, Graphics g, float phase, float walk)
    {
        float bob = 0.014f * MathF.Sin(2 * phase + 1f) * walk;
        float hip = 0.31f + bob;

        // ---- the curled tail, held over the back, bouncing with the stride ----
        {
            float cx = -0.30f, cy = 0.55f + bob + 0.022f * MathF.Sin(2 * phase + 0.4f) * walk;
            ink.Ellipse(cx, cy, 0.13f, 0.125f, Coat);
            if (ink.Colours)
            {
                ink.Ellipse(cx + 0.015f, cy - 0.005f, 0.078f, 0.075f, Cream, 0, false);
                ink.Ellipse(cx + 0.03f, cy - 0.002f, 0.04f, 0.04f, Coat, 0, false);
            }
        }

        // ---- far legs (darker), then the body, then near legs ----
        void Leg(float hx, float phi, bool front, Color upper, Color lower)
        {
            float uu = ((phi % MathF.Tau) + MathF.Tau) % MathF.Tau;
            float fx, lift;
            if (uu < MathF.PI) { fx = 1 - 2 * uu / MathF.PI; lift = 0; }                         // stance: slide back along the ground
            else { float q = (uu - MathF.PI) / MathF.PI; fx = -1 + 2 * Smooth(q); lift = 0.075f * MathF.Sin(q * MathF.PI); }   // swing: forward through the air
            PointF hipP = P(hx, hip);
            PointF foot = P(hx + Reach * walk * fx, lift * walk);
            float dx = foot.X - hipP.X, dy = foot.Y - hipP.Y;
            float d = MathF.Min(MathF.Sqrt(dx * dx + dy * dy), 2 * Bone - 0.002f);
            float alpha = MathF.Acos(Math.Clamp(d / (2 * Bone), -1, 1));
            float theta = MathF.Atan2(dy, dx) + (front ? -alpha : alpha);                          // elbows point back, hind knees point forward
            PointF knee = P(hipP.X + Bone * MathF.Cos(theta), hipP.Y + Bone * MathF.Sin(theta));
            ink.Limb(hipP, knee, front ? 0.135f : 0.155f, upper);
            ink.Limb(P(knee.X + (foot.X - knee.X) * 0.22f, knee.Y + (foot.Y - knee.Y) * 0.22f), foot, 0.095f, lower);                                                  // cream "socks"
            ink.Ellipse(foot.X + 0.02f, foot.Y + 0.026f, 0.062f, 0.04f, lower);                   // the paw
        }
        float frontX = 0.21f, hindX = -0.20f;
        Leg(frontX, phase + MathF.PI, true, CoatFar, CreamFar);
        Leg(hindX, phase, false, CoatFar, CreamFar);

        // ---- body: cream belly peeking below the orange back, cream chest ----
        ink.Ellipse(0.02f, 0.325f + bob, 0.25f, 0.075f, Cream);
        ink.Ellipse(-0.02f, 0.41f + bob, 0.315f, 0.145f, Coat, 2f);
        ink.Ellipse(0.22f, 0.45f + bob, 0.14f, 0.14f, Coat);                                      // shoulders
        ink.Ellipse(0.30f, 0.40f + bob, 0.075f, 0.115f, Cream, 8f);                               // cream chest

        // ---- the head ----
        {
            float hb = 0.008f * MathF.Sin(2 * phase + 0.3f) * walk;
            float hx = 0.405f, hy = 0.56f + bob + hb;
            // far ear, skull, cheek ruff, muzzle, near ear
            ink.Blob([P(hx - 0.09f, hy + 0.08f), P(hx - 0.11f, hy + 0.255f), P(hx - 0.005f, hy + 0.12f)], CoatFar, false);
            ink.Ellipse(hx, hy, 0.15f, 0.135f, Coat);
            ink.Ellipse(hx + 0.095f, hy - 0.04f, 0.085f, 0.06f, Cream, -8f);                      // the muzzle
            ink.Ellipse(hx + 0.015f, hy - 0.07f, 0.095f, 0.06f, Cream, 6f);                       // the cheek ruff
            ink.Blob([P(hx - 0.04f, hy + 0.10f), P(hx - 0.005f, hy + 0.27f), P(hx + 0.07f, hy + 0.11f)], Coat, false);   // near ear
            if (ink.Colours)
            {
                using var inner = new SolidBrush(Color.FromArgb(255, 226, 200));
                g.FillPolygon(inner, [P(hx - 0.012f, hy + 0.125f), P(hx - 0.002f, hy + 0.225f), P(hx + 0.04f, hy + 0.13f)]);
                // nose
                ink.Detail(hx + 0.178f, hy - 0.012f, 0.026f, 0.021f, Ink);
                // eye: dark, round, with a bright highlight
                ink.Detail(hx + 0.07f, hy + 0.035f, 0.025f, 0.03f, Ink);
                ink.Detail(hx + 0.079f, hy + 0.047f, 0.009f, 0.009f, Color.White);
                // a little cream spot above the eye, the Shiba's "eyebrow"
                ink.Detail(hx + 0.06f, hy + 0.085f, 0.014f, 0.011f, Cream);
                // a smile line from the nose, and a small pink tongue
                using var pen = new Pen(Ink, 0.012f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                g.DrawCurve(pen, [P(hx + 0.172f, hy - 0.04f), P(hx + 0.15f, hy - 0.085f), P(hx + 0.115f, hy - 0.1f), P(hx + 0.085f, hy - 0.078f)]);
                ink.Detail(hx + 0.125f, hy - 0.112f, 0.02f, 0.016f, Color.FromArgb(240, 120, 130));
                // blush
                using var blush = new SolidBrush(Color.FromArgb(70, 255, 130, 120));
                g.FillEllipse(blush, hx + 0.015f, hy - 0.045f, 0.075f, 0.045f);
            }
        }

        Leg(frontX, phase, true, Coat, Cream);
        Leg(hindX, phase + MathF.PI, false, Coat, Cream);
    }

    // ----------------------------------------------------------------- paint: front view

    private Sprite Front(float wag, float tiltDeg) =>
        Sprite.Paint(_sw, _sh, g =>
        {
            g.TranslateTransform(_sw / 2f, _sh - 5);
            g.ScaleTransform(_k, -_k);
            float edge = 1.3f / _k;
            using (var shadow = new SolidBrush(Color.FromArgb(70, 20, 30, 14)))
                g.FillEllipse(shadow, -0.30f, -0.035f, 0.60f, 0.08f);
            for (int pass = 0; pass < 2; pass++) FrontDog(new Ink_(g, pass, edge), g, wag, tiltDeg);
        });

    private static void FrontDog(Ink_ ink, Graphics g, float wag, float tiltDeg)
    {
        // ---- the curled tail, to one side of the haunches, wagging ----
        {
            float sway = 0.045f * MathF.Sin(wag);
            float cx = -0.255f + sway, cy = 0.20f + 0.012f * MathF.Cos(wag * 2);
            ink.Ellipse(cx, cy, 0.105f, 0.11f, Coat);
            if (ink.Colours)
            {
                ink.Ellipse(cx + 0.012f, cy, 0.062f, 0.066f, Cream, 0, false);
                ink.Ellipse(cx + 0.02f, cy, 0.03f, 0.032f, Coat, 0, false);
            }
        }

        // ---- haunches, body, chest, front legs ----
        ink.Ellipse(-0.165f, 0.115f, 0.115f, 0.115f, Coat);
        ink.Ellipse(0.165f, 0.115f, 0.115f, 0.115f, Coat);
        ink.Ellipse(0.0f, 0.29f, 0.19f, 0.27f, Coat);
        ink.Ellipse(0.0f, 0.30f, 0.105f, 0.215f, Cream);
        ink.Limb(P(-0.075f, 0.28f), P(-0.08f, 0.06f), 0.095f, Cream);
        ink.Limb(P(0.075f, 0.28f), P(0.08f, 0.06f), 0.095f, Cream);
        ink.Ellipse(-0.085f, 0.035f, 0.065f, 0.038f, Cream);
        ink.Ellipse(0.085f, 0.035f, 0.065f, 0.038f, Cream);
        if (ink.Colours)
        {
            using var toe = new Pen(Color.FromArgb(150, 160, 120, 90), 0.008f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            foreach (float sx in new[] { -1f, 1f })
            {
                g.DrawLine(toe, P(sx * 0.085f - 0.012f, 0.012f), P(sx * 0.085f - 0.012f, 0.04f));
                g.DrawLine(toe, P(sx * 0.085f + 0.012f, 0.012f), P(sx * 0.085f + 0.012f, 0.04f));
            }
        }

        // ---- the head, tipped about the neck ----
        var save = g.Save();
        g.TranslateTransform(0, 0.50f);
        g.RotateTransform(-tiltDeg);                        // y is up, so a negative turn tips the head to the viewer's right
        g.TranslateTransform(0, -0.50f);

        // ears first (behind the skull), orange with a cream inside
        ink.Blob([P(-0.205f, 0.72f), P(-0.215f, 0.945f), P(-0.065f, 0.80f)], Coat, false);
        ink.Blob([P(0.205f, 0.72f), P(0.215f, 0.945f), P(0.065f, 0.80f)], Coat, false);
        if (ink.Colours)
        {
            using var inner = new SolidBrush(Color.FromArgb(255, 226, 202));
            g.FillPolygon(inner, [P(-0.185f, 0.76f), P(-0.19f, 0.895f), P(-0.10f, 0.795f)]);
            g.FillPolygon(inner, [P(0.185f, 0.76f), P(0.19f, 0.895f), P(0.10f, 0.795f)]);
        }
        // skull: wide and round, the big head is the cute part
        ink.Ellipse(0f, 0.66f, 0.225f, 0.175f, Coat);
        if (ink.Colours)
        {
            // cream cheeks, with fluffy points at the sides, clipped to the head
            using var skull = new GraphicsPath();
            skull.AddEllipse(-0.225f, 0.485f, 0.45f, 0.35f);
            var s2 = g.Save();
            g.SetClip(skull, CombineMode.Intersect);
            using var cheeks = new GraphicsPath();
            cheeks.AddClosedCurve([P(-0.24f, 0.63f), P(-0.15f, 0.595f), P(-0.07f, 0.60f), P(0f, 0.585f), P(0.07f, 0.60f), P(0.15f, 0.595f), P(0.24f, 0.63f),
                                   P(0.23f, 0.54f), P(0.12f, 0.47f), P(0f, 0.46f), P(-0.12f, 0.47f), P(-0.23f, 0.54f)], 0.45f);
            using (var cb = new SolidBrush(Cream)) g.FillPath(cb, cheeks);
            g.Restore(s2);
        }
        // blush
        if (ink.Colours)
        {
            using var blush = new SolidBrush(Color.FromArgb(85, 255, 120, 120));
            g.FillEllipse(blush, -0.20f, 0.555f, 0.09f, 0.055f);
            g.FillEllipse(blush, 0.11f, 0.555f, 0.09f, 0.055f);
        }
        // muzzle bump
        ink.Ellipse(0f, 0.57f, 0.095f, 0.06f, Cream, 0, false);
        if (ink.Colours)
        {
            // open smiling mouth: a dark half-moon under the nose, a pink tongue in it, and a thin
            // line above it; the little dimples at each end give the famous Shiba grin
            using (var b = new SolidBrush(Color.FromArgb(120, 44, 52))) g.FillPie(b, -0.07f, 0.505f, 0.14f, 0.095f, 0, 180);
            using (var b = new SolidBrush(Color.FromArgb(246, 122, 138))) g.FillEllipse(b, -0.034f, 0.512f, 0.068f, 0.04f);
            using (var pen = new Pen(Ink, 0.012f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                g.DrawArc(pen, -0.07f, 0.505f, 0.14f, 0.095f, 0, 180);
                g.DrawLine(pen, P(0f, 0.585f), P(0f, 0.600f));
                g.DrawArc(pen, -0.095f, 0.575f, 0.03f, 0.03f, 200, 110);
                g.DrawArc(pen, 0.065f, 0.575f, 0.03f, 0.03f, 230, 110);
            }            // nose
            using (var nb = new SolidBrush(Ink))
                g.FillPolygon(nb, [P(-0.036f, 0.605f), P(0.036f, 0.605f), P(0.0f, 0.568f)]);
            using (var nb = new SolidBrush(Ink)) g.FillEllipse(nb, -0.039f, 0.592f, 0.078f, 0.036f);
            using (var hb = new SolidBrush(Color.FromArgb(150, 255, 255, 255))) g.FillEllipse(hb, -0.012f, 0.612f, 0.02f, 0.008f);
            // eyes: dark round, two highlights; and a cream spot above each, the Shiba's "eyebrows"
            foreach (float sx in new[] { -1f, 1f })
            {
                using (var eb = new SolidBrush(Ink)) g.FillEllipse(eb, sx * 0.082f - 0.03f, 0.64f, 0.06f, 0.07f);
                using (var wb = new SolidBrush(Color.White))
                {
                    g.FillEllipse(wb, sx * 0.082f - 0.004f, 0.675f, 0.022f, 0.022f);
                    g.FillEllipse(wb, sx * 0.082f - 0.02f, 0.648f, 0.01f, 0.01f);
                }
                using (var cb = new SolidBrush(Cream)) g.FillEllipse(cb, sx * 0.082f - 0.022f, 0.725f, 0.044f, 0.03f);
            }
        }
        g.Restore(save);
    }
}
