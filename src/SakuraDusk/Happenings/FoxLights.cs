using Peckworks.Screensavers.Core;

namespace SakuraDusk.Happenings;

/// <summary>
/// Kitsune-bi, "fox fire": in old Japanese stories, a row of small lights
/// drifting along the hills at dusk is a fox wedding on its way. Eight to
/// fourteen warm lights appear one by one at one side, walk slowly along the
/// middle range of hills in a loose line, each bobbing as if carried by an
/// unseen hand, and fade away one by one at the far side. Not scary: warm and
/// a little magical.
///
/// WHERE THEY MAY BE SEEN. The lights belong ON the middle hills: in front of
/// the far range, behind the near range and the pagoda. So the constructor
/// builds a stencil (a yes/no sheet, one answer per pixel of the screen) that
/// is true only where the middle range shows: below the middle ridge line,
/// above the near ridge line, and not on anything painted nearer (the
/// pagoda, branches). Think of a card with a window cut in it, held in front
/// of the picture: a light can only be seen through the window.
///
/// HOW THE LINE MOVES. Every light has its own "distance walked" along the
/// path: speed * t minus its place in the queue. Light number 0 leads, the
/// rest are further back. A light that has not yet walked into the path is
/// not shown, one that has walked off the end is gone, and each fades in and
/// out over the first and last stretch of the path. So they appear and
/// vanish one at a time, with nothing popping.
/// </summary>
internal sealed class FoxLights : Happening
{
    private const int MaxLights = 14;
    private static readonly Color Gold = Color.FromArgb(255, 190, 110);

    private readonly DuskScenery _s;
    private readonly bool[] _window;                   // true where the middle hills are on show (see the summary)
    private readonly Sprite _halo, _core;
    private readonly float _pathStart, _pathEnd;       // the stretch of screen the procession walks, left to right
    private readonly bool _enoughHill;                 // is the middle range actually visible for most of the path on this seed?

    // This showing's dice (rolled in Begin).
    private int _count;
    private bool _rightward;
    private float _speed;                              // pixels per second
    private readonly float[] _behind = new float[MaxLights];   // how far back in the queue each light walks, in pixels
    private readonly float[] _bobPhase = new float[MaxLights];
    private readonly float[] _swayPhase = new float[MaxLights];
    private readonly float[] _flickerPhase = new float[MaxLights];
    private readonly float[] _flickerRate = new float[MaxLights];

    public override float Seconds => 16f;
    public override bool CanBegin => _enoughHill;

    public FoxLights(DuskScenery s)
    {
        _s = s;
        int w = s.Width, h = s.Height;
        float u = s.U;
        _pathStart = w * 0.10f;
        _pathEnd = w * 0.90f;

        // ---- the window ----
        _window = new bool[w * h];
        PointF[] mid = s.Ridges[1], near = s.Ridges[2];
        float pagodaHalf = u * 0.045f;
        int good = 0, total = 0;
        for (int x = 0; x < w; x++)
        {
            float top = RidgeAt(mid, x), bottom = RidgeAt(near, x);
            if (x >= _pathStart && x <= _pathEnd)
            {
                total++;
                if (bottom - top > u * 0.008f) good++;
            }
            bool inPagoda = MathF.Abs(x - s.Pagoda.X) < pagodaHalf;
            int y0 = Math.Max(0, (int)MathF.Ceiling(top)), y1 = Math.Min(h - 1, (int)MathF.Floor(bottom));
            for (int y = y0; y <= y1; y++)
            {
                if (inPagoda && y >= s.Pagoda.Top - 2 && y <= s.Pagoda.Base) continue;   // the pagoda stands in front of the lights
                if (s.OpenWater[y * w + x]) _window[y * w + x] = true;                   // not a tree, a branch or a bank
            }
        }
        _enoughHill = total > 0 && good > total * 0.55f;

        _halo = Sprite.Glow(Math.Max(6, (int)(u * 0.034f)), Gold);
        _core = Sprite.Glow(Math.Max(3, (int)(u * 0.013f)), Color.FromArgb(255, 226, 160));
    }

    /// <summary>
    /// The height of a ridge at x, smoothly: a straight line between the two
    /// outline points either side. (Brushwork.RidgeYAt picks the NEAREST
    /// point, so a light walking along it would step. This one slides.)
    /// The first and last points of a ridge outline are closing corners.
    /// </summary>
    private static float RidgeAt(PointF[] r, float x)
    {
        int last = r.Length - 2;                       // the last real point
        if (x <= r[1].X) return r[1].Y;
        for (int i = 2; i <= last; i++)
            if (x <= r[i].X)
            {
                float k = (x - r[i - 1].X) / Math.Max(0.001f, r[i].X - r[i - 1].X);
                return r[i - 1].Y + (r[i].Y - r[i - 1].Y) * k;
            }
        return r[last].Y;
    }

    public override void Begin(Random rng)
    {
        _count = rng.Next(8, MaxLights + 1);
        _rightward = rng.Next(2) == 0;
        float u = _s.U;

        // A loose queue: gaps of 0.035 to 0.06 U, never even.
        float behind = 0;
        for (int i = 0; i < MaxLights; i++)
        {
            _behind[i] = behind;
            behind += u * (0.035f + 0.025f * (float)rng.NextDouble());
            _bobPhase[i] = (float)(rng.NextDouble() * Math.PI * 2);
            _swayPhase[i] = (float)(rng.NextDouble() * Math.PI * 2);
            _flickerPhase[i] = (float)(rng.NextDouble() * Math.PI * 2);
            _flickerRate[i] = 2.2f + 2.5f * (float)rng.NextDouble();
        }

        // Speed: just enough that the LAST light finishes the path a second before the end.
        _speed = (_pathEnd - _pathStart + _behind[_count - 1]) / (Seconds - 1.2f);
    }

    public override void Draw(FrameBuffer fb, float t)
    {
        float u = _s.U;
        float len = _pathEnd - _pathStart;
        float ramp = u * 0.10f;                         // the stretch over which a light fades in or out
        float bobAmp = Math.Max(1.5f, u * 0.006f), swayAmp = Math.Max(1.2f, u * 0.004f);

        for (int i = 0; i < _count; i++)
        {
            float walked = _speed * t - _behind[i];
            if (walked <= 0 || walked >= len) continue;
            float fade = Smooth(walked / ramp) * Smooth((len - walked) / ramp);
            if (fade <= 0.01f) continue;

            float x = _rightward ? _pathStart + walked : _pathEnd - walked;
            x += swayAmp * MathF.Sin(t * 1.3f + _swayPhase[i]);

            float top = RidgeAt(_s.Ridges[1], x), bottom = RidgeAt(_s.Ridges[2], x);
            float band = bottom - top;
            if (band <= 0) continue;
            // A little way down the hillside, but never further than half the visible slice of it.
            float y = top + Math.Min(u * 0.014f, band * 0.5f);
            y += bobAmp * MathF.Sin(t * 5.2f + _bobPhase[i]) * 0.5f + bobAmp * 0.5f * MathF.Sin(t * 2.1f + _bobPhase[i] * 1.7f);

            // A soft flicker: two slow waves of different speeds, never dropping low.
            float flicker = 0.82f + 0.10f * MathF.Sin(t * _flickerRate[i] + _flickerPhase[i])
                                  + 0.08f * MathF.Sin(t * 7.3f + _flickerPhase[i] * 2.3f);
            float a = fade * flicker;

            x = MathF.Round(x);
            y = MathF.Round(y);
            _halo.DrawCentered(fb, x, y, 0.75f * a, _window);
            _core.DrawCentered(fb, x, y, 0.95f * a, _window);
        }
    }
}
