using System.Drawing.Drawing2D;
using Peckworks.Screensavers.Core;

namespace Thanksgiving.Happenings;

/// <summary>
/// A giant inflatable turkey balloon, the kind that floats down a Thanksgiving
/// parade, drifts slowly across the top of the sky: plump and glossy, a fan
/// of soft candy-coloured tail feathers, a pilgrim hat, big friendly eyes,
/// bobbing gently and tilting a little as it goes, with three tether lines
/// hanging from its feet and tail and trailing down behind the hills, where
/// the handlers are.
///
/// It is cheap on purpose (it is about 0.2 U across and a big stamp is the
/// dearest thing in a sky): ONE stamp a frame. The tilt comes from a small
/// flip-book of nine pages, each the same balloon painted already leaned
/// by a degree more than the one before, from -4 to +4 degrees. The bob (up
/// and down) is just moving the stamp. The three lines are thin lines drawn
/// from where the balloon's feet are NOW (so they tilt and bob with it).
/// The balloon faces right; going the other way, the same stamp is turned
/// over (mirrored).
///
/// Drawn through the OpenSky stencil: the corner branches pass in front of
/// it and the lines end behind the hills. Claims "costly".
/// </summary>
internal sealed class ParadeBalloon : Happening
{
    private const int Pages = 9;
    private const float MaxTilt = 4f;                               // degrees, either way
    private const float Seconds_ = 25f;

    private readonly ThanksgivingScenery _s;
    private readonly Sprite[] _page = new Sprite[Pages];
    private readonly int _w, _h;                                    // each page's size in pixels
    private readonly float _bw;                                     // the balloon's own width: the unit of every painted coordinate
    private readonly PointF[] _tether = [new(0.36f, 0.93f), new(0.60f, 0.93f), new(0.14f, 0.80f)];   // where the three lines tie on, as fractions of the page, balloon facing right

    private bool _flip;                                             // true = drifting left, so the balloon is mirrored
    private float _y0, _bobPhase, _tiltPhase, _swayPhase;
    private float _startX, _endX;                                   // the page's CENTRE x at the start and the end

    public override float Seconds => Seconds_;
    public override string? Claims => "costly";

    public ParadeBalloon(ThanksgivingScenery s)
    {
        _s = s;
        _bw = Math.Max(24f, s.U * 0.2f);
        _w = (int)(_bw * 1.25f);
        _h = (int)(_bw * 1.3f);
        for (int i = 0; i < Pages; i++)
            _page[i] = PaintBalloon((i - (Pages - 1) / 2f) / ((Pages - 1) / 2f) * MaxTilt);
    }

    /// <summary>The balloon leaned by "tilt" degrees (clockwise positive) round the middle of its page.</summary>
    private Sprite PaintBalloon(float tilt)
    {
        return Sprite.Paint(_w, _h, g =>
        {
            g.TranslateTransform(_w / 2f, _h / 2f);
            g.RotateTransform(tilt);
            g.TranslateTransform(-_w / 2f, -_h / 2f);
            // From here, paint in a box of "b" by "b" pixels placed in the middle of the page (the balloon's
            // own size), with (0, 0) its top left corner and (1, 1) its bottom right corner.
            float b = _bw;
            g.TranslateTransform((_w - b) / 2f, (_h - b * 1.0f) / 2f);
            g.ScaleTransform(b, b);

            // ---- The tail: seven rounded feathers fanned out behind and above the body ----
            Color[] feather =
            [
                Color.FromArgb(232, 112, 100), Color.FromArgb(244, 158, 84), Color.FromArgb(248, 208, 98),
                Color.FromArgb(120, 190, 172), Color.FromArgb(248, 208, 98), Color.FromArgb(244, 158, 84), Color.FromArgb(232, 112, 100),
            ];
            PointF hub = new(0.40f, 0.56f);
            for (int i = 0; i < 7; i++)
            {
                float ang = (-172f + i * 22f) * MathF.PI / 180f;      // the fan runs from left-low round the top to up-right
                var state = g.Save();
                g.TranslateTransform(hub.X, hub.Y);
                g.RotateTransform(ang * 180f / MathF.PI);
                // A feather is a fat oval along the x axis, from near the hub out to the radius.
                RectangleF rect = new(0.05f, -0.085f, 0.40f, 0.17f);
                Inflated(g, rect, feather[i], 0.012f);
                // A paler "eye" band near the tip of each feather, like a printed pattern.
                RectangleF tip = new(0.27f, -0.05f, 0.14f, 0.10f);
                using (var tb = new SolidBrush(Color.FromArgb(150, 255, 244, 220))) g.FillEllipse(tb, tip);
                g.Restore(state);
            }

            // ---- The body: a plump round ball, a warm brown, shaded to look blown up ----
            RectangleF body = new(0.17f, 0.40f, 0.52f, 0.46f);
            Inflated(g, body, Color.FromArgb(190, 120, 72), 0.014f);
            // A lighter belly patch
            using (var belly = new SolidBrush(Color.FromArgb(70, 255, 226, 180)))
                g.FillEllipse(belly, 0.27f, 0.58f, 0.32f, 0.24f);
            // Two wing bumps on the body's side
            using (var wing = new SolidBrush(Color.FromArgb(160, 100, 58)))
                g.FillEllipse(wing, 0.20f, 0.52f, 0.22f, 0.20f);

            // ---- Feet: two stubby orange ovals under the body ----
            Inflated(g, new RectangleF(0.28f, 0.84f, 0.16f, 0.09f), Color.FromArgb(246, 164, 60), 0.008f);
            Inflated(g, new RectangleF(0.52f, 0.84f, 0.16f, 0.09f), Color.FromArgb(246, 164, 60), 0.008f);

            // ---- Neck and head: a round head up and to the right, with a big shiny look ----
            Inflated(g, new RectangleF(0.56f, 0.30f, 0.24f, 0.30f), Color.FromArgb(190, 120, 72), 0.012f);    // the neck
            RectangleF head = new(0.58f, 0.18f, 0.26f, 0.25f);
            Inflated(g, head, Color.FromArgb(200, 132, 82), 0.012f);
            // Red wattle hanging under the beak
            Inflated(g, new RectangleF(0.76f, 0.34f, 0.055f, 0.10f), Color.FromArgb(222, 70, 66), 0.006f);
            // Orange beak
            using (var beak = new SolidBrush(Color.FromArgb(250, 176, 54)))
            using (var rim = new Pen(Color.FromArgb(120, 130, 70, 20), 0.006f))
            {
                PointF[] bk = [new(0.82f, 0.27f), new(0.93f, 0.315f), new(0.82f, 0.36f)];
                g.FillPolygon(beak, bk);
                g.DrawPolygon(rim, bk);
            }
            // Big friendly eye: white, a dark pupil looking forward, a tiny highlight
            using (var white = new SolidBrush(Color.White))
            using (var pupil = new SolidBrush(Color.FromArgb(40, 26, 24)))
            using (var shine = new SolidBrush(Color.White))
            using (var lid = new Pen(Color.FromArgb(120, 90, 50, 30), 0.006f))
            {
                g.FillEllipse(white, 0.715f, 0.235f, 0.075f, 0.085f);
                g.DrawEllipse(lid, 0.715f, 0.235f, 0.075f, 0.085f);
                g.FillEllipse(pupil, 0.745f, 0.258f, 0.04f, 0.045f);
                g.FillEllipse(shine, 0.765f, 0.262f, 0.014f, 0.014f);
            }
            // Rosy cheek
            using (var cheek = new SolidBrush(Color.FromArgb(70, 255, 120, 110)))
                g.FillEllipse(cheek, 0.68f, 0.31f, 0.06f, 0.04f);

            // ---- The pilgrim hat: black, a buckle in gold, tipped a little ----
            var hatState = g.Save();
            g.TranslateTransform(0.69f, 0.19f);
            g.RotateTransform(-8f);
            using (var hat = new SolidBrush(Color.FromArgb(44, 38, 52)))
            using (var hatEdge = new Pen(Color.FromArgb(110, 255, 255, 255), 0.004f))
            {
                g.FillPolygon(hat, [new PointF(-0.075f, 0f), new PointF(-0.055f, -0.13f), new PointF(0.055f, -0.13f), new PointF(0.075f, 0f)]);
                g.FillEllipse(hat, -0.115f, -0.02f, 0.23f, 0.04f);                                  // the brim
                g.DrawLine(hatEdge, -0.05f, -0.12f, -0.05f, -0.03f);                                // a glint down the left side of the crown
                using var band = new SolidBrush(Color.FromArgb(180, 120, 70));
                g.FillRectangle(band, -0.07f, -0.045f, 0.14f, 0.03f);
                using var buckle = new SolidBrush(Color.FromArgb(250, 210, 80));
                g.FillRectangle(buckle, -0.02f, -0.052f, 0.04f, 0.044f);
                using var hole = new SolidBrush(Color.FromArgb(180, 120, 70));
                g.FillRectangle(hole, -0.011f, -0.043f, 0.022f, 0.026f);
            }
            g.Restore(hatState);
        });
    }

    /// <summary>
    /// An inflated shape: a filled oval with a darker soft rim round its lower right, and a bright glossy
    /// highlight up at its upper left. The two together are what make a flat oval read as a blown-up balloon.
    /// "rim" is the width of the rim in painted units.
    /// </summary>
    private static void Inflated(Graphics g, RectangleF r, Color c, float rim)
    {
        using var shape = new GraphicsPath();
        shape.AddEllipse(r);
        using (var fill = new SolidBrush(c)) g.FillPath(fill, shape);
        // Shade: a gradient from clear in the lit upper left to dark at the lower right, clipped to the shape.
        var clip = g.Save();
        g.SetClip(shape, CombineMode.Intersect);
        using (var shade = new LinearGradientBrush(new PointF(r.X + r.Width * 0.2f, r.Y + r.Height * 0.1f),
                                                   new PointF(r.Right, r.Bottom),
                                                   Color.FromArgb(0, 40, 10, 30), Color.FromArgb(95, 60, 20, 50)))
            g.FillRectangle(shade, r.X - 0.01f, r.Y - 0.01f, r.Width + 0.02f, r.Height + 0.02f);
        // The highlight: a small soft white oval near the top left.
        RectangleF hl = new(r.X + r.Width * 0.14f, r.Y + r.Height * 0.10f, r.Width * 0.38f, r.Height * 0.26f);
        using (var hp = new GraphicsPath())
        {
            hp.AddEllipse(hl);
            using var hb = new PathGradientBrush(hp) { CenterColor = Color.FromArgb(150, 255, 255, 255), SurroundColors = [Color.FromArgb(0, 255, 255, 255)] };
            g.FillPath(hb, hp);
        }
        g.Restore(clip);
        // A soft darker outline, thin.
        using var pen = new Pen(Color.FromArgb(90, 70, 30, 40), Math.Max(rim * 0.5f, 0.004f));
        g.DrawPath(pen, shape);
    }

    public override void Begin(Random rng)
    {
        float W = _s.Width, H = _s.Height, U = _s.U;
        _flip = rng.Next(2) == 0;
        // High in the sky, with the bobbing room above and the hills well below.
        _y0 = Math.Max(_h * 0.5f + U * 0.02f, H * (0.14f + 0.06f * (float)rng.NextDouble()));
        _bobPhase = (float)rng.NextDouble() * MathF.Tau;
        _tiltPhase = (float)rng.NextDouble() * MathF.Tau;
        _swayPhase = (float)rng.NextDouble() * MathF.Tau;
        float off = _w * 0.5f + 4;
        _startX = _flip ? W + off : -off;
        _endX = _flip ? -off : W + off;
    }

    public override void Draw(FrameBuffer fb, float t)
    {
        float U = _s.U;
        float k = t / Seconds_;                                       // 0 to 1 across the showing; a steady drift
        float cx = _startX + (_endX - _startX) * k;
        float bob = MathF.Max(1.5f, U * 0.009f) * MathF.Sin(t * 1.35f + _bobPhase);
        float cy = _y0 + bob;
        float tilt = MaxTilt * MathF.Sin(t * 0.9f + _tiltPhase);      // the tilt now, in degrees (a slow lean)
        int page = Math.Clamp((int)MathF.Round(tilt / MaxTilt * (Pages - 1) / 2f + (Pages - 1) / 2f), 0, Pages - 1);
        float fade = Fade(t, Seconds_, 0.5f, 0.5f);

        // The three lines: from each tie point (turned by the tilt about the page's middle) down to a point
        // far below, which sways a little. The hills hide the lower end through the stencil.
        int left = (int)(cx - _w / 2f), top = (int)(cy - _h / 2f);
        float rad = tilt * MathF.PI / 180f * (_flip ? -1 : 1), sin = MathF.Sin(rad), cos = MathF.Cos(rad);
        float lineThick = Math.Max(1f, U * 0.0016f);
        float bw = _bw, offX = (_w - bw) / 2f, offY = (_h - bw) / 2f;
        for (int i = 0; i < _tether.Length; i++)
        {
            // The tie point in page pixels (mirrored when the balloon faces left), then turned by the tilt.
            float px = (_tether[i].X * bw + offX), py = (_tether[i].Y * bw + offY);
            if (_flip) px = _w - px;
            float dx = px - _w / 2f, dy = py - _h / 2f;
            float tx = left + _w / 2f + dx * cos - dy * sin, ty = top + _h / 2f + dx * sin + dy * cos;
            // The line runs to a point well below, drifting a little in the direction of travel (the handlers walk
            // behind) and swaying on its own slow wave.
            float lean = (_flip ? 1 : -1) * U * 0.10f + U * 0.02f * MathF.Sin(t * 0.8f + _swayPhase + i * 1.7f);
            PointF a = new(tx, ty);
            PointF mid = new(tx + lean * 0.35f + U * 0.008f * MathF.Sin(t * 1.1f + i * 2.1f + _swayPhase), ty + (_s.Horizon - ty) * 0.5f);
            PointF b = new(tx + lean + (i - 1) * U * 0.05f, _s.Horizon + U * 0.05f);
            fb.Line(a, mid, Color.FromArgb(250, 235, 210), 0.55f * fade, lineThick, _s.OpenSky);
            fb.Line(mid, b, Color.FromArgb(250, 235, 210), 0.45f * fade, lineThick, _s.OpenSky);
        }

        _page[page].Draw(fb, left, top, fade, _s.OpenSky, _flip);
    }
}
