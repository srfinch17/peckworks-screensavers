using System.Drawing.Drawing2D;
using Peckworks.Screensavers.Core;

namespace SakuraDusk.Happenings;

/// <summary>
/// Ichiban-boshi: "the first star" of the evening, the one Japanese children
/// make a wish on. In the dark upper sky one bright star fades in, sparkles,
/// and while it shines a handful of fainter stars come out one by one around
/// it. Then they all slip away.
///
/// FEYNMAN VERSION: a sparkle is a plus sign of light (four thin arms). Real
/// stars do not turn, but our eye sees a star "twinkle" when its arms seem
/// to shimmer. We fake that cheaply with TWO plus signs: one upright, one
/// turned 45 degrees (an X), and we let them take turns being brighter. The
/// eye blends the two into a star whose arms slowly seem to turn. A soft
/// blue-white halo sits behind, and breathes a little.
///
/// The first star is bigger and brighter than the rest, with a larger halo,
/// so it is clearly the special one. Where each star goes is decided in Begin
/// and checked against the stencil, so none lands on a branch or a hill.
/// </summary>
internal sealed class FirstStar : Happening
{
    private readonly DuskScenery _s;
    private readonly Sprite _bigPlus, _bigX, _bigHalo;            // the first star: upright arms, turned arms, halo
    private readonly Sprite _smallPlus, _smallX, _smallHalo;      // the fainter ones
    private float _seconds = 14f;
    private PointF _first;
    private (PointF At, float Phase, float Rate, float Delay)[] _others = [];

    public override float Seconds => _seconds;
    public override string? Claims => "night,clouds";

    public FirstStar(DuskScenery s)
    {
        _s = s;
        float u = s.U;
        int big = Math.Max(6, (int)(u * 0.042f));                 // arm length of the first star, in pixels
        int small = Math.Max(3, (int)(u * 0.011f));
        _bigPlus = Plus(big, 0f, 1f);
        _bigX = Plus((int)(big * 0.62f), MathF.PI / 4, 1f);
        _bigHalo = Halo(Math.Max(10, (int)(u * 0.075f)), 150);
        _smallPlus = Plus(small, 0f, 0.9f);
        _smallX = Plus((int)(small * 0.7f), MathF.PI / 4, 0.9f);
        _smallHalo = Halo(Math.Max(5, (int)(u * 0.022f)), 110);
    }

    /// <summary>
    /// A four-armed sparkle: a thin concave star, with its arms pointing out
    /// along the axes (turned by "turn" radians). Long thin tips, a fat
    /// middle, and a tiny bright dot in the center.
    /// </summary>
    private static Sprite Plus(int arm, float turn, float bright)
    {
        int size = arm * 2 + 4;
        return Sprite.Paint(size, size, g =>
        {
            float c = size / 2f;
            var pts = new PointF[8];
            for (int i = 0; i < 8; i++)
            {
                // Even points are the four tips; odd points are the pinched
                // "waist" between two arms, which is what makes the arms thin.
                float a = i * MathF.PI / 4 + turn;
                float r = i % 2 == 0 ? arm : arm * 0.13f;
                pts[i] = new PointF(c + r * MathF.Cos(a), c + r * MathF.Sin(a));
            }
            using var soft = new SolidBrush(Color.FromArgb((int)(70 * bright), 200, 220, 255));
            using var core = new SolidBrush(Color.FromArgb((int)(255 * bright), 255, 255, 255));
            // A slightly fatter, faint copy underneath softens the edges.
            float k = 1.5f;
            var fat = new PointF[8];
            for (int i = 0; i < 8; i++) fat[i] = new PointF(c + (pts[i].X - c) * (i % 2 == 0 ? 1f : k), c + (pts[i].Y - c) * (i % 2 == 0 ? 1f : k));
            g.FillPolygon(soft, fat);
            g.FillPolygon(core, pts);
            float dot = Math.Max(1.2f, arm * 0.16f);
            g.FillEllipse(core, c - dot, c - dot, dot * 2, dot * 2);
        });
    }

    /// <summary>A soft round blue-white halo, with no hot spot (the sparkle is the hot spot).</summary>
    private static Sprite Halo(int radius, int centerAlpha) =>
        Sprite.Paint(radius * 2, radius * 2, g =>
        {
            using var path = new GraphicsPath();
            path.AddEllipse(0, 0, radius * 2, radius * 2);
            using var b = new PathGradientBrush(path)
            {
                CenterColor = Color.FromArgb(centerAlpha, 190, 210, 255),
                SurroundColors = [Color.FromArgb(0, 190, 210, 255)],
                Blend = Sprite.SoftFalloff,
            };
            g.FillPath(b, path);
        });

    /// <summary>True when the pixel at (x, y) and a ring of points around it are all bare sky.</summary>
    private bool Clear(float x, float y, float radius)
    {
        if (x < 0 || y < 0 || x >= _s.Width || y >= _s.Height) return false;
        if (!_s.OpenSky[(int)y * _s.Width + (int)x]) return false;
        for (int k = 0; k < 12; k++)
        {
            float a = k * MathF.Tau / 12;
            int px = (int)(x + radius * MathF.Cos(a)), py = (int)(y + radius * MathF.Sin(a));
            if (px < 0 || py < 0 || px >= _s.Width || py >= _s.Height || !_s.OpenSky[py * _s.Width + px]) return false;
        }
        return true;
    }

    public override void Begin(Random rng)
    {
        float u = _s.U, w = _s.Width, h = _s.Height;
        _seconds = 14f;
        _first = default;
        bool found = false;
        for (int i = 0; i < 80 && !found; i++)
        {
            float x = w * (0.12f + 0.76f * (float)rng.NextDouble()), y = h * (0.06f + 0.16f * (float)rng.NextDouble());
            if (Clear(x, y, u * 0.05f)) { _first = new PointF(x, y); found = true; }
        }
        if (!found) { _seconds = 0.3f; _others = []; return; }  // no clear patch of dark sky: end at once (rule 14)

        // The fainter stars: 5 to 9, each in clear sky, not too near the first star or each other.
        int want = rng.Next(5, 10);
        var list = new List<(PointF At, float Phase, float Rate, float Delay)>();
        for (int tries = 0; tries < 300 && list.Count < want; tries++)
        {
            float x = _first.X + u * (float)(rng.NextDouble() * 1.4 - 0.7), y = h * (0.03f + 0.37f * (float)rng.NextDouble());
            float dFirst = MathF.Sqrt((x - _first.X) * (x - _first.X) + (y - _first.Y) * (y - _first.Y));
            if (dFirst < u * 0.12f || !Clear(x, y, Math.Max(2f, u * 0.012f))) continue;
            if (list.Any(o => (o.At.X - x) * (o.At.X - x) + (o.At.Y - y) * (o.At.Y - y) < u * u * 0.0064f)) continue;   // at least 0.08 U apart
            list.Add((new PointF(x, y), (float)(rng.NextDouble() * Math.Tau), 1.2f + 1.6f * (float)rng.NextDouble(), 4.5f + list.Count * 0.65f));
        }
        _others = list.ToArray();
    }

    public override void Draw(FrameBuffer fb, float t)
    {
        if (_seconds < 1f) return;
        float end = Smooth((Seconds - t) / 3f);                   // everything slips away over the last 3 seconds
        float appear = Smooth(t / 3.5f) * end;

        // The first star: the two sparkles take turns, the halo breathes.
        float pulse = 0.85f + 0.15f * (float)Math.Sin(t * 2.4);
        float swap = 0.5f + 0.5f * (float)Math.Sin(t * 1.3);      // 0..1: which sparkle is brighter right now
        _bigHalo.DrawCentered(fb, _first.X, _first.Y, 0.6f * appear * pulse, _s.OpenSky);
        _bigPlus.DrawCentered(fb, _first.X, _first.Y, appear * (0.65f + 0.35f * swap), _s.OpenSky);
        _bigX.DrawCentered(fb, _first.X, _first.Y, appear * (0.45f + 0.55f * (1 - swap)), _s.OpenSky);

        foreach (var o in _others)
        {
            float a = Smooth((t - o.Delay) / 1.4f) * end;
            if (a <= 0) continue;
            float tw = 0.65f + 0.35f * (float)Math.Sin(t * o.Rate + o.Phase);
            _smallHalo.DrawCentered(fb, o.At.X, o.At.Y, 0.35f * a * tw, _s.OpenSky);
            _smallPlus.DrawCentered(fb, o.At.X, o.At.Y, 0.85f * a * tw, _s.OpenSky);
            _smallX.DrawCentered(fb, o.At.X, o.At.Y, 0.5f * a * (1.3f - tw), _s.OpenSky);
        }
    }
}
