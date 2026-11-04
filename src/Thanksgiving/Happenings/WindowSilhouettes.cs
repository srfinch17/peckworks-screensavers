using System.Drawing.Drawing2D;
using Peckworks.Screensavers.Core;

namespace Thanksgiving.Happenings;

/// <summary>
/// Dinner is being got ready inside the farmhouse. Behind the two downstairs
/// windows, dark figures pass against the yellow lamplight: one walks past the
/// left window, stops a moment and nods, then goes on; the same person comes
/// by the other window carrying a big platter with the roast bird on it,
/// pauses to show it off and carries it on. Meanwhile a second person (with a
/// bun of hair) walks to the first window, raises a glass, clinks it and
/// lowers it, and walks out the other way.
///
/// FEYNMAN VERSION: a window is only about 17 pixels wide on a full HD screen,
/// so a person in it is a very small shadow puppet. A puppet show is just a
/// flip-book. Every picture (a "page") is exactly the size of the window and is
/// stamped on the very pixels of the window, so the figure can never poke out
/// of the glass: whatever the page does not cover, the picture of the window
/// underneath shows through. To make a person walk across, we paint a long
/// row of pages, each with the figure a little further along; the page we show
/// is picked from t, like a flick book. The pauses are small separate sets of
/// pages (a nod, the platter lifting, an arm going up with a glass).
///
/// The window's cross bars are part of the backdrop, and a person standing
/// behind the glass is behind the bars. So after the figure is painted, the
/// bars are painted over it again, in the same colour, in the same place
/// (done pixel by pixel in Compose, so the bars are not drawn twice on the
/// bare glass and do not get darker there). The scene stamps the lamp glow over
/// the window afterwards, which is right: the light is in front of the shadow.
///
/// Which way round it all happens (left window first, or right window first)
/// is picked in Begin. The windows are far too small to show a person in the
/// 152 by 112 preview box, so there CanBegin is false and the director deals
/// something else.
/// </summary>
internal sealed class WindowSilhouettes : Happening
{
    private const float Total = 10f;
    // The timetable (seconds). A walks in front of the first window, nods, walks on.
    private const float AIn = 0.4f, APause = 1.6f, AOut = 3.0f, AEnd = 4.0f;
    // A, now carrying the platter, crosses the second window.
    private const float CIn = 3.8f, CPause = 5.0f, COut = 6.2f, CEnd = 7.4f;
    // B walks into the first window, lifts a glass, clinks, lowers it, walks out.
    private const float BIn = 5.0f, BStop = 6.0f, BUp = 6.9f, BDown = 8.3f, BStill = 9.0f, BEnd = 9.9f;

    private const int PausePages = 8, CarryPausePages = 6, ToastPages = 10;
    private static readonly Color Dark = Color.FromArgb(54, 32, 18);
    private static readonly Color Skin = Color.FromArgb(54, 32, 18);
    private static readonly Color Platter = Color.FromArgb(120, 82, 48);
    private static readonly Color Roast = Color.FromArgb(176, 102, 38);
    private static readonly Color Glass = Color.FromArgb(150, 96, 52);
    private static readonly Color BarColor = Color.FromArgb(120, 100, 80);          // the painter's cross-bar colour (PaintFarmhouse)

    private readonly ThanksgivingScenery _s;
    private readonly bool _fits;
    private readonly int[] _left = new int[2], _top = new int[2];                    // each window's sprite, top-left on the screen
    private readonly int _np;                                                        // pages in one crossing
    // Pages, by [which way round (d)]: window "first" gets A walking, A's nod, B walking, B's toast; window "second" gets A carrying, A's platter lift.
    private readonly Sprite[][] _walkA = new Sprite[2][], _pauseA = new Sprite[2][], _walkB = new Sprite[2][], _toastB = new Sprite[2][];
    private readonly Sprite[][] _carry = new Sprite[2][], _carryPause = new Sprite[2][];

    private int _d;                                                                  // 0 = A walks left to right (left window first), 1 = right to left

    public override float Seconds => _fits ? Total : 0.2f;
    public override string? Claims => "farmhouse";
    public override bool CanBegin => _fits;

    private int First => _d == 0 ? 0 : 1;
    private int Second => 1 - First;

    public WindowSilhouettes(ThanksgivingScenery s)
    {
        _s = s;
        RectangleF w0 = s.FarmWindows[0];
        _fits = w0.Width >= 8f && w0.Height >= 8f;                   // a person needs a window at least this big to be seen at all
        _np = _fits ? Math.Clamp((int)(w0.Width * 2.5f), 24, 90) : 1;
        if (!_fits)
        {
            // Nothing will ever be drawn; keep the arrays valid so nothing can index into nothing.
            for (int d = 0; d < 2; d++) { _walkA[d] = _pauseA[d] = _walkB[d] = _toastB[d] = _carry[d] = _carryPause[d] = []; }
            return;
        }

        for (int i = 0; i < 2; i++)
        {
            RectangleF w = s.FarmWindows[i];
            _left[i] = (int)MathF.Floor(w.X) - 1;                    // one spare pixel all round, which the clip keeps clear
            _top[i] = (int)MathF.Floor(w.Y) - 1;
        }

        for (int d = 0; d < 2; d++)
        {
            int first = d == 0 ? 0 : 1, second = 1 - first;
            float dirA = d == 0 ? 1f : -1f, dirB = -dirA;

            _walkA[d] = new Sprite[_np];
            _walkB[d] = new Sprite[_np];
            _carry[d] = new Sprite[_np];
            for (int i = 0; i < _np; i++)
            {
                float k = i / (float)(_np - 1);
                _walkA[d][i] = Page(first, new Pose { S = k, Dir = dirA });
                _walkB[d][i] = Page(first, new Pose { S = k, Dir = dirB, Bun = true });
                _carry[d][i] = Page(second, new Pose { S = k, Dir = dirA, Platter = 0f });
            }
            _pauseA[d] = new Sprite[PausePages];
            for (int i = 0; i < PausePages; i++)
                _pauseA[d][i] = Page(first, new Pose { S = 0.5f, Dir = dirA, Nod = i / (float)PausePages });
            _carryPause[d] = new Sprite[CarryPausePages];
            for (int i = 0; i < CarryPausePages; i++)
                _carryPause[d][i] = Page(second, new Pose { S = 0.5f, Dir = dirA, Platter = MathF.Sin(MathF.PI * (i + 0.5f) / CarryPausePages) });
            // B's toast: eight pages of the arm going up, then two with the glass tilted either way (the clink).
            _toastB[d] = new Sprite[ToastPages];
            for (int i = 0; i < ToastPages; i++)
            {
                float a = Math.Min(i, 7) / 7f;
                float tilt = i == 8 ? -14f : i == 9 ? 14f : 0f;
                _toastB[d][i] = Page(first, new Pose { S = 0.5f, Dir = dirB, Bun = true, Arm = a, Tilt = tilt });
            }
        }
    }

    public override void Begin(Random rng) => _d = rng.Next(2);

    // ----------------------------------------------------------------- paint

    /// <summary>Everything that tells one page of the flip-book apart from another.</summary>
    private struct Pose
    {
        public float S;          // how far across the window the person is: 0 just off one side, 1 just off the other
        public float Dir;        // +1 walking right, -1 walking left (which way they face)
        public bool Bun;         // the second person has her hair in a bun
        public float Nod;        // 0 to 1: one nod of the head while standing still (-1 when not nodding)
        public float Platter;    // -1 = no platter; 0 to 1 = carrying one, held this far up
        public float Arm;        // 0 to 1: how far the arm holding a glass has been raised
        public float Tilt;       // degrees the glass is tipped (the clink)
        public Pose() { Nod = -1f; Platter = -1f; }
    }

    /// <summary>
    /// One page: a sprite exactly the size of the window (plus a spare pixel round
    /// it), the figure painted inside the window's glass only, the cross bars on top.
    /// </summary>
    private Sprite Page(int win, Pose p)
    {
        RectangleF w = _s.FarmWindows[win];
        int left = _left[win], top = _top[win];
        int sw = (int)MathF.Ceiling(w.Right) + 1 - left, sh = (int)MathF.Ceiling(w.Bottom) + 1 - top;
        float wl = w.X - left, wt = w.Y - top, ww = w.Width, H = w.Height;

        Sprite figure = Sprite.Paint(sw, sh, g =>
        {
            // The glass, a little inset so the frame's own edge pixels are never touched.
            g.SetClip(new RectangleF(wl + 0.8f, wt + 0.8f, ww - 1.6f, H - 1.6f));

            // Across the window: from wholly outside on one side to wholly outside on the other.
            float xLo = wl - 0.30f * H, xHi = wl + ww + 0.30f * H;
            float cx = p.Dir > 0 ? xLo + (xHi - xLo) * p.S : xHi - (xHi - xLo) * p.S;

            // Walking: the body rises and dips as each foot lands, four steps across the whole crossing
            // (an even number, so the figure is level at the middle, where it stops).
            float bob = -0.045f * H * MathF.Abs(MathF.Sin(p.S * 4 * MathF.PI));
            float headDx = 0f, headDy = 0f;
            if (p.Nod >= 0)                                          // a nod and a glance at the other window
            {
                headDy = 0.05f * H * MathF.Sin(MathF.PI * p.Nod);
                headDx = p.Dir * 0.035f * H * MathF.Sin(MathF.Tau * p.Nod);
            }
            if (p.Arm > 0) headDy = -0.04f * H * p.Arm;               // head tips back for the toast

            float headR = H * 0.14f;
            float headY = wt + H * 0.38f + bob + headDy;
            float shY = wt + H * 0.54f + bob;
            float rx = H * 0.31f;
            using var dark = new SolidBrush(Dark);
            using var skin = new SolidBrush(Skin);

            // Body: tall rounded shoulders, as if the window cut the person off at the chest.
            g.FillEllipse(dark, cx - rx, shY, rx * 2, H * 2f);
            g.FillRectangle(skin, cx - H * 0.05f, headY + headR * 0.6f, H * 0.10f, shY - headY);        // neck
            g.FillEllipse(skin, cx + headDx - headR, headY - headR, headR * 2, headR * 2.1f);           // head
            if (p.Bun) g.FillEllipse(skin, cx + headDx - p.Dir * headR * 0.9f - headR * 0.45f, headY - headR * 1.25f, headR * 0.9f, headR * 0.9f);

            // A platter, held out in front with both hands: a flat dish with the roast bird on it.
            if (p.Platter >= 0)
            {
                float py = shY + H * 0.24f - H * 0.07f * p.Platter;
                float px = cx + p.Dir * H * 0.40f;
                using var arm = new Pen(Dark, Math.Max(1.2f, H * 0.10f)) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                g.DrawLine(arm, cx + p.Dir * H * 0.12f, shY + H * 0.10f, px - p.Dir * H * 0.10f, py);
                using var dish = new SolidBrush(Platter);
                g.FillEllipse(dish, px - H * 0.34f, py - H * 0.04f, H * 0.68f, H * 0.10f);
                using var bird = new SolidBrush(Roast);
                g.FillEllipse(bird, px - H * 0.20f, py - H * 0.17f, H * 0.40f, H * 0.20f);                // the bird
                g.FillEllipse(bird, px + p.Dir * H * 0.13f, py - H * 0.23f, H * 0.10f, H * 0.10f);        // a drumstick poking up
            }

            // The toast: the near arm goes up with a glass in its hand.
            if (p.Arm > 0 || p.Tilt != 0)
            {
                float a = p.Arm;
                PointF sh0 = new(cx + p.Dir * H * 0.20f, shY + H * 0.10f);
                PointF hand = new(sh0.X + p.Dir * H * (0.07f + 0.15f * a), sh0.Y + H * (0.26f - 0.56f * a));
                using var arm = new Pen(Dark, Math.Max(1.2f, H * 0.10f)) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                g.DrawLine(arm, sh0, hand);
                if (a > 0.5f)
                {
                    var st = g.Save();
                    g.TranslateTransform(hand.X, hand.Y);
                    g.RotateTransform(p.Tilt * p.Dir);
                    using var glass = new SolidBrush(Glass);
                    float gw = Math.Max(2f, H * 0.16f), gh = Math.Max(2.4f, H * 0.19f);
                    g.FillPolygon(glass, [new PointF(-gw / 2, -gh), new PointF(gw / 2, -gh), new PointF(gw * 0.25f, 0), new PointF(-gw * 0.25f, 0)]);
                    g.Restore(st);
                }
            }
        });

        // The bars, as the painter drew them: a vertical and a horizontal line through the middle.
        Sprite bars = Sprite.Paint(sw, sh, g =>
        {
            using var pen = new Pen(BarColor, Math.Max(1f, _s.U * 0.0015f));
            g.DrawLine(pen, wl + ww / 2, wt, wl + ww / 2, wt + H);
            g.DrawLine(pen, wl, wt + H / 2, wl + ww, wt + H / 2);
        });
        return Compose(figure, bars);
    }

    /// <summary>
    /// Lays the bars over the figure, only where the figure is: a pixel with figure keeps
    /// the figure's coverage, but its colour moves toward the bar colour by the bar's coverage.
    /// Pixels with no figure stay clear, so the backdrop's own bars show there, once.
    /// </summary>
    private static Sprite Compose(Sprite figure, Sprite bars)
    {
        var fig = figure.Argb;
        var bar = bars.Argb;
        var result = new uint[fig.Length];
        for (int i = 0; i < result.Length; i++)
        {
            uint f = fig[i];
            int fa = (int)(f >> 24);
            if (fa == 0) continue;
            uint b = bar[i];
            int ba = (int)(b >> 24);
            int r = (int)((f >> 16) & 0xFF), g = (int)((f >> 8) & 0xFF), bl = (int)(f & 0xFF);
            // The sprite's colours are stored premultiplied by nothing (straight alpha), so a plain blend is right.
            r += (BarColor.R - r) * ba / 255;
            g += (BarColor.G - g) * ba / 255;
            bl += (BarColor.B - bl) * ba / 255;
            result[i] = (uint)((fa << 24) | (r << 16) | (g << 8) | bl);
        }
        return Sprite.FromPixels(figure.Width, figure.Height, result);
    }

    // ------------------------------------------------------------------ draw

    /// <summary>The way across one window, 0 to 1, as the clock runs: slowing into a stop, or setting off from one.</summary>
    private static float Ease(float t, float from, float to) => Math.Clamp((t - from) / (to - from), 0f, 1f);

    public override void Draw(FrameBuffer fb, float t)
    {
        if (!_fits) return;
        int d = _d, first = First, second = Second;

        // ---- A, in front of the first window ----
        if (t >= AIn && t < AEnd)
        {
            Sprite page;
            if (t < APause) { float p = Ease(t, AIn, APause); page = _walkA[d][Idx(0.5f * p * (2 - p))]; }            // slows to a stop
            else if (t < AOut) page = _pauseA[d][Math.Min(PausePages - 1, (int)(Ease(t, APause, AOut) * PausePages))];   // one nod
            else { float p = Ease(t, AOut, AEnd); page = _walkA[d][Idx(0.5f + 0.5f * p * p)]; }                       // sets off again
            page.Draw(fb, _left[first], _top[first]);
        }

        // ---- A again, carrying the platter past the second window ----
        if (t >= CIn && t < CEnd)
        {
            Sprite page;
            if (t < CPause) { float p = Ease(t, CIn, CPause); page = _carry[d][Idx(0.5f * p * (2 - p))]; }
            else if (t < COut) page = _carryPause[d][Math.Min(CarryPausePages - 1, (int)(Ease(t, CPause, COut) * CarryPausePages))];
            else { float p = Ease(t, COut, CEnd); page = _carry[d][Idx(0.5f + 0.5f * p * p)]; }
            page.Draw(fb, _left[second], _top[second]);
        }

        // ---- B, in the first window: walks in, toasts, walks out the far side ----
        if (t >= BIn && t < BEnd)
        {
            Sprite page;
            if (t < BStop) { float p = Ease(t, BIn, BStop); page = _walkB[d][Idx(0.5f * p * (2 - p))]; }
            else if (t < BUp) page = _toastB[d][Math.Min(7, (int)(Ease(t, BStop, BUp) * 8))];                          // the arm goes up
            else if (t < BDown)                                                                                        // holds, with a little clink (the glass tips each way, twice) near the start
            {
                float u = t - BUp;
                page = _toastB[d][u > 0.3f && u < 0.9f ? ((int)(u / 0.15f) % 2 == 0 ? 8 : 9) : 7];
            }
            else if (t < BStill) page = _toastB[d][Math.Min(7, (int)((1f - Ease(t, BDown, BStill)) * 8))];             // lowers
            else { float p = Ease(t, BStill, BEnd); page = _walkB[d][Idx(0.5f + 0.5f * p * p)]; }
            page.Draw(fb, _left[first], _top[first]);
        }
    }

    /// <summary>The page number for how far across (0 to 1) a figure is.</summary>
    private int Idx(float s) => Math.Clamp((int)MathF.Round(s * (_np - 1)), 0, _np - 1);
}
