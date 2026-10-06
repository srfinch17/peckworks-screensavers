using Peckworks.Screensavers.Core;

namespace Christmas.Happenings;

/// <summary>
/// A sudden gust of wind sweeps a swirl of snow across the whole screen, from
/// one side to the other (a random side each time), in about six seconds.
///
/// It is 150 to 250 small white dots, a few larger blurry flakes (nearest the
/// viewer, so they move fastest), all riding one invisible moving "front":
/// a line sweeping across the screen. Think of leaves in a gust of wind.
///
/// Each dot rolled its own numbers in Begin: how far behind the front it
/// rides (its "lag"), its height on the screen, its speed (a little above or
/// below the front's), the size of the little circle it whirls in, and the
/// phase of its wobble. Draw then works out where each one is from t:
///   - Along the screen: speed * t minus its lag. Negative means "still off
///     the edge, waiting to blow in".
///   - Up and down: its lane, plus a slow wave that travels along the band
///     (so the whole sheet of snow ripples), plus a small circle it whirls
///     around (so it reads as a swirl, not a straight line of dots).
///   - Brightness: fades in as it blows on to the screen and out as it
///     leaves.
///
/// No stencil: the gust is in front of everything, like the falling snow.
/// </summary>
internal sealed class SnowGust : Happening
{
    private const int Max = 250, Big = 10;

    private readonly ChristmasScenery _s;
    private readonly Sprite[] _dot = new Sprite[3];                // three sizes of small soft dot
    private readonly Sprite _blur;                                 // the big blurry flake

    private int _n;
    private float _dir;                                            // +1 blows left to right, -1 right to left
    private readonly float[] _lag = new float[Max + Big], _lane = new float[Max + Big], _speed = new float[Max + Big];
    private readonly float[] _radius = new float[Max + Big], _spin = new float[Max + Big], _phase = new float[Max + Big];
    private readonly int[] _size = new int[Max + Big];             // 0..2 small dots, 3 = big blurry flake

    public override float Seconds => 6f;

    public SnowGust(ChristmasScenery s)
    {
        _s = s;
        var white = Color.FromArgb(250, 252, 255);
        _dot[0] = Sprite.Glow(Math.Max(2, (int)(s.U * 0.005f)), white);
        _dot[1] = Sprite.Glow(Math.Max(2, (int)(s.U * 0.006f)), white);
        _dot[2] = Sprite.Glow(Math.Max(3, (int)(s.U * 0.009f)), white);
        _blur = Sprite.Glow(Math.Max(4, (int)(s.U * 0.026f)), white);
    }

    public override void Begin(Random rng)
    {
        _dir = rng.Next(2) == 0 ? 1 : -1;
        _n = 150 + rng.Next(101) + Big;                            // 150 to 250 dots, plus the big flakes
        for (int i = 0; i < _n; i++)
        {
            bool big = i >= _n - Big;
            _size[i] = big ? 3 : rng.Next(3);
            _lag[i] = 0.02f + 0.18f * (float)rng.NextDouble();     // as a fraction of the screen width
            _lane[i] = 0.05f + 0.90f * (float)rng.NextDouble();    // height, as a fraction of the screen height
            _speed[i] = (big ? 1.25f : 0.85f + 0.3f * (float)rng.NextDouble()) * 0.25f;   // screen widths per second
            _radius[i] = (big ? 0.05f : 0.03f + 0.10f * (float)rng.NextDouble());       // size of its little whirl, in U
            _spin[i] = (2.5f + 3.5f * (float)rng.NextDouble()) * (big ? 0.5f : 1f);       // how fast it whirls, radians per second
            _phase[i] = (float)rng.NextDouble() * MathF.Tau;
        }
    }

    /// <summary>Where dot i is at time t, and how visible it is (0 = not on screen).</summary>
    private (float X, float Y, float Vis) Where(int i, float t)
    {
        float W = _s.Width, H = _s.Height, U = _s.U;
        // How far across the screen it is, 0 (entry edge) to 1 (exit edge); below 0 it is still waiting off-screen.
        float along = _speed[i] * t - _lag[i];
        if (along < -0.05f || along > 1.05f) return (0, 0, 0);

        // Fade in over the first part of the width and out over the last.
        float vis = Math.Min(Smooth((along + 0.05f) / 0.11f), Smooth((1.05f - along) / 0.13f));

        // The ripple that travels along the sheet, plus this dot's own whirl.
        double wave = U * 0.09 * Math.Sin(along * 9 + (double)t * 2.4 + _phase[i] * 0.2);
        double spin = _spin[i] * (double)t + _phase[i];
        float x = (_dir > 0 ? along : 1 - along) * W + (float)(Math.Cos(spin) * _radius[i] * U);
        float y = _lane[i] * H + (float)(wave + Math.Sin(spin) * _radius[i] * U * 0.8);
        return (x, y, vis);
    }

    public override void Draw(FrameBuffer fb, float t)
    {
        float U = _s.U;
        for (int i = 0; i < _n; i++)
        {
            var (x, y, vis) = Where(i, t);
            if (vis <= 0.01f) continue;

            if (_size[i] == 3)
            {
                _blur.DrawCentered(fb, x, y, vis * 0.6f);
                continue;
            }
            // A short streak behind it, along where it was 0.03 s ago: it is wind, so things smear.
            var (px, py, _) = Where(i, t - 0.03f);
            if (_size[i] > 0)
                fb.Line(new PointF(px, py), new PointF(x, y), Color.FromArgb(240, 246, 255), vis * 0.35f, Math.Max(1f, U * (0.0015f + 0.001f * _size[i])));
            _dot[_size[i]].DrawCentered(fb, x, y, vis);
        }
    }
}
