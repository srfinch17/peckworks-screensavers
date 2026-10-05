using System.Drawing.Drawing2D;
using Peckworks.Screensavers.Core;
using Peckworks.Screensavers.Core.Sakura;

namespace Halloween.Happenings;

/// <summary>
/// Something huge peeks over the far hill. A giant shadowy head and shoulders
/// rises slowly from behind the ridge, wakes up (two big yellow eyes open),
/// looks left, then right, blinks, and sinks back out of sight.
///
/// The trick that makes it cheap: the hill is what hides the monster. We use
/// the OpenSky stencil ("only draw where the sky is open"), so wherever the
/// hill is, our stamp is simply not drawn. All we do is slide ONE tall
/// picture up and down; the ridge cuts it off for free. Like a puppet on a
/// stick behind a cardboard hill in a school play.
///
/// The pieces, all painted once in the constructor:
///   - BODY: the dark purple head (a dome with two curved horns) merging
///     into broad shoulders, as one tall sheet reaching well below the ridge.
///   - GLOW: a soft yellow halo behind each eye.
///   - EYES: both eyes in one small sprite. There are 3 looks (pupils to the
///     left, center, right) times 8 lid positions (wide open to shut), a
///     flip-book for the blink and for the eyes opening and closing.
///
/// Sizes (in u): the head and horns are about 0.15 wide, the shoulders 0.24.
/// At the top of its rise the eyes sit 0.05 u above the ridge and the top of
/// the skull 0.10 u above it.
/// </summary>
internal sealed class HillMonster : Happening
{
    private const int Lids = 8;                            // lid positions per look: 0 = shut ... 7 = wide open
    private static readonly Color Hide = Color.FromArgb(22, 12, 36);   // very dark purple, darker than the hill

    private readonly HalloweenScenery _s;
    private readonly Sprite _body, _halo;
    private readonly Sprite[,] _eyes;                      // [look, lid]
    private readonly int _bodyW, _bodyH, _hornH;           // the body sheet's size, and how far the horn tips stand above the skull's top
    private readonly int _eyeW, _eyeH;
    private readonly float _eyeSpread;                     // each eye's distance from the middle, in pixels
    private readonly float _eyeDown;                       // eye height below the top of the skull, in pixels

    // This showing's choices.
    private float _cx;                                     // the monster's middle x
    private float _topRisen, _topHidden;                   // the body sheet's top y when fully up, and when fully hidden

    public override float Seconds => 12f;

    // The timeline, in seconds.
    private const float RiseFrom = 0.3f, RiseTo = 2.8f;    // rises over 2.5 s
    private const float SinkFrom = 9.2f, SinkTo = 11.7f;   // sinks over 2.5 s

    public HillMonster(HalloweenScenery s)
    {
        _s = s;
        float u = s.U;
        _hornH = Math.Max(3, (int)(u * 0.05f));
        _bodyW = Math.Max(12, (int)(u * 0.34f));
        _bodyH = Math.Max(24, (int)(u * 0.45f));
        float mid = _bodyW / 2f, skull = _hornH;           // x of the middle, and y of the skull's top, inside the sheet

        _body = Sprite.Paint(_bodyW, _bodyH, g =>
        {
            using var dark = new SolidBrush(Hide);
            PointF P(float ax, float ay) => new(mid + u * ax, skull + u * ay);   // (across, down) from the middle of the skull's top, in u

            // The shoulders: they start at the sides of the head, slope out
            // and down, then run straight down to the bottom of the sheet.
            g.FillPolygon(dark, [P(-0.065f, 0.075f), P(-0.120f, 0.105f), P(-0.150f, 0.150f), new PointF(mid - u * 0.150f, _bodyH),
                                 new PointF(mid + u * 0.150f, _bodyH), P(0.150f, 0.150f), P(0.120f, 0.105f), P(0.065f, 0.075f)]);
            // The head: a dome, a little wider than tall, so it looks heavy and a bit dim-witted.
            g.FillEllipse(dark, mid - u * 0.078f, skull, u * 0.156f, u * 0.19f);
            // Two curved horns: each is a thin curved blade, wide at the skull and sharp at the tip, bending outward.
            foreach (int side in (int[])[-1, 1])
            {
                using var horn = new GraphicsPath();
                PointF H(float ax, float ay) => P(side * ax, ay);
                horn.AddBezier(H(0.060f, 0.045f), H(0.092f, 0.020f), H(0.125f, -0.010f), H(0.112f, -0.048f));   // the outer edge, up to the tip
                horn.AddBezier(H(0.112f, -0.048f), H(0.096f, -0.020f), H(0.066f, -0.004f), H(0.028f, 0.010f));  // back down the inner edge
                horn.CloseFigure();
                g.FillPath(dark, horn);
            }
        });

        // The eyes. Both go in one sheet. Each is an almond (a lens shape)
        // tilted so the inner ends are low: it makes the face look grumpy.
        _eyeSpread = u * 0.034f;
        _eyeDown = u * 0.050f;
        float ew = u * 0.034f, eh = u * 0.021f;
        _eyeW = (int)(_eyeSpread * 2 + ew * 1.6f) + 4;
        _eyeH = (int)(eh * 2.2f) + 6;
        _eyes = new Sprite[3, Lids];
        for (int look = 0; look < 3; look++)
            for (int lid = 0; lid < Lids; lid++)
                _eyes[look, lid] = PaintEyes(look - 1, lid / (Lids - 1f), ew, eh);

        _halo = Sprite.Glow(Math.Max(4, (int)(u * 0.05f)), Color.FromArgb(255, 200, 30));
    }

    /// <summary>
    /// Paints both eyes. "look" is -1 (looking left), 0 or +1 (right); "open"
    /// is 0 (shut) to 1 (wide open). Each eye is a yellow almond with a
    /// slit pupil that slides toward where it is looking, and a lid (the
    /// body's own dark color) that comes down from the top to cover it.
    /// </summary>
    private Sprite PaintEyes(int look, float open, float ew, float eh) => Sprite.Paint(_eyeW, _eyeH, g =>
    {
        float mid = _eyeW / 2f, cy = _eyeH / 2f;
        foreach (int side in (int[])[-1, 1])
        {
            GraphicsState before = g.Save();
            g.TranslateTransform(mid + side * _eyeSpread, cy);
            g.RotateTransform(side * 14f);                       // the inner end tips down: left eye turns one way, right the other

            using var almond = new GraphicsPath();
            almond.AddBezier(-ew / 2, 0, -ew * 0.2f, -eh, ew * 0.2f, -eh, ew / 2, 0);   // the upper curve
            almond.AddBezier(ew / 2, 0, ew * 0.2f, eh, -ew * 0.2f, eh, -ew / 2, 0);     // the lower curve
            almond.CloseFigure();
            g.SetClip(almond);

            using (var iris = new LinearGradientBrush(new PointF(0, -eh), new PointF(0, eh), Color.FromArgb(255, 240, 120), Color.FromArgb(255, 170, 20)))
                g.FillRectangle(iris, -ew, -eh * 1.5f, ew * 2, eh * 3);

            // The slit pupil: a tall thin oval, slid sideways by the look.
            float px = look * ew * 0.22f, pw = Math.Max(1.5f, ew * 0.14f);
            using (var pupil = new SolidBrush(Color.FromArgb(24, 8, 30)))
                g.FillEllipse(pupil, px - pw / 2, -eh * 0.95f, pw, eh * 1.9f);

            // The lid: a dark sheet from the top down to "open". Wide open = lid out of the way; shut = it covers the whole eye.
            float lidBottom = -eh + (1 - open) * eh * 2.1f;
            using (var lid = new SolidBrush(Hide))
                g.FillRectangle(lid, -ew, -eh * 2, ew * 2, lidBottom + eh * 2);
            g.Restore(before);
        }
    });

    public override void Begin(Random rng)
    {
        float u = _s.U;
        // Anywhere from 0.10 to 0.90 of the width, but not behind the house:
        // at least 0.21 u away from it (a bit over the 0.16 u minimum, so the horns clear the tower). Try a few random spots, then fall
        // back to the far side from the house (which always satisfies it
        // unless the screen is tiny).
        //
        // It also keeps clear of the dead tree if it can. Behind the tree's
        // thin twigs a big dark shape shows a fault of the stencil: a twig's
        // soft edge pixels are part twig, part bright sky, the stencil leaves
        // them alone, and against the dark monster they light up as a thin
        // bright rim round every twig. So the first 40 tries want both rules;
        // only if no spot passes both (a narrow screen) is the tree let go.
        float treeLeft = _s.DeadTreeFoot.X, treeRight = _s.DeadTreeFoot.X;
        foreach (PointF spot in _s.DeadTreeSpots)
        {
            treeLeft = MathF.Min(treeLeft, spot.X);
            treeRight = MathF.Max(treeRight, spot.X);
        }
        _cx = -1;
        for (int tries = 0; tries < 80 && _cx < 0; tries++)
        {
            float x = _s.Width * (0.10f + 0.80f * (float)rng.NextDouble());
            bool clearOfHouse = MathF.Abs(x - _s.House.X) >= u * 0.21f;
            bool clearOfTree = x + _bodyW / 2f < treeLeft || x - _bodyW / 2f > treeRight;
            if (clearOfHouse && (clearOfTree || tries >= 40)) _cx = x;
        }
        if (_cx < 0) _cx = _s.House.X < _s.Width / 2f ? _s.Width * 0.9f : _s.Width * 0.1f;

        // The highest bit of ridge under the head sets how high the head must go; the lowest bit under the whole sheet sets where it hides.
        float highest = float.MaxValue, lowest = float.MinValue;
        for (float x = _cx - _bodyW / 2f; x <= _cx + _bodyW / 2f; x += Math.Max(1, _bodyW / 40f))
        {
            float y = Brushwork.RidgeYAt(_s.FarRidge, Math.Clamp(x, 0, _s.Width - 1));
            lowest = MathF.Max(lowest, y);
            if (MathF.Abs(x - _cx) <= u * 0.075f) highest = MathF.Min(highest, y);
        }
        // Risen: the skull's top 0.10 u above the highest ridge point under the head (the horns stand above that).
        _topRisen = highest - u * 0.10f - _hornH;
        // Hidden: even the horn tips below the lowest ridge point.
        _topHidden = lowest + 3;
    }

    /// <summary>How far up it is, 0 (hidden) to 1 (fully risen), at time t.</summary>
    private static float Up(float t) => Math.Min(Smooth((t - RiseFrom) / (RiseTo - RiseFrom)), Smooth((SinkTo - t) / (SinkTo - SinkFrom)));

    public override void Draw(FrameBuffer fb, float t)
    {
        float up = Up(t);
        if (up <= 0) return;

        // A slow breathing sway of a couple of pixels, rounded ONCE so the body and eyes move together.
        float breath = MathF.Sin(t * 1.6f) * Math.Max(1.5f, _s.U * 0.002f) * Smooth(up);
        int left = (int)MathF.Round(_cx - _bodyW / 2f), top = (int)MathF.Round(_topHidden + (_topRisen - _topHidden) * up + breath);
        _body.Draw(fb, left, top, 1f, _s.OpenSky);

        // ---- the eyes: where to look, and how open the lids are ----
        // The eyes open once it has mostly risen and close again as it sinks.
        float awake = Math.Min(Smooth((t - 2.5f) / 0.7f), Smooth((SinkFrom + 0.6f - t) / 0.5f));
        // Looking: center, then left, then right, then center again, each move eased.
        float gaze = Math.Clamp(
            -Smooth((t - 3.6f) / 0.4f) + Smooth((t - 5.3f) / 0.4f) * 2 - Smooth((t - 7.0f) / 0.4f) - Smooth((t - 8.2f) / 0.3f) * 0, -1f, 1f);
        int look = gaze < -0.5f ? 0 : gaze > 0.5f ? 2 : 1;
        // Blinks: at 5.0 s (between looking left and right) and at 7.7 s. A blink closes and reopens the lids in 0.3 s.
        float blink = Math.Max(Pulse(t, 5.0f, 0.3f), Pulse(t, 7.7f, 0.3f));
        float open = awake * (1 - blink);
        int lid = Math.Clamp((int)MathF.Round(open * (Lids - 1)), 0, Lids - 1);

        // The eyes sit at the same whole-number offsets from the body's corner each frame.
        float centerX = left + _bodyW / 2f;
        int eyeLeft = (int)(centerX - _eyeW / 2f), eyeTop = (int)(top + _hornH + _eyeDown - _eyeH / 2f);
        if (lid > 0)
        {
            float glow = open * 0.75f;
            _halo.DrawCentered(fb, centerX - _eyeSpread, eyeTop + _eyeH / 2f, glow, _s.OpenSky);
            _halo.DrawCentered(fb, centerX + _eyeSpread, eyeTop + _eyeH / 2f, glow, _s.OpenSky);
            _eyes[look, lid].Draw(fb, eyeLeft, eyeTop, 1f, _s.OpenSky);
        }
    }

    /// <summary>A little bump that rises to 1 and falls back to 0 over "width" seconds, centered on "at". Used for a blink.</summary>
    private static float Pulse(float t, float at, float width)
    {
        float k = (t - (at - width / 2)) / width;
        return k <= 0 || k >= 1 ? 0 : MathF.Sin(k * MathF.PI);
    }
}
