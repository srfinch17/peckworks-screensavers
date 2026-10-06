using Peckworks.Screensavers.Core;

namespace SakuraDusk.Happenings;

/// <summary>
/// A cat's-paw: a puff of breeze that wrinkles a patch of the pond as it
/// crosses. You can see it on a still lake as a darker, glittering patch
/// racing over the glass-smooth water.
///
/// FEYNMAN VERSION: the patch is just a crowd of tiny dashes (a pale one
/// catches the light, a dark one is a little shadowed slope). Each dash has
/// its own place INSIDE the band, and its own blink: it flickers on and off at
/// its own speed, so the whole patch shimmers. The band moves; the dashes ride
/// with it. They are crowded in the middle of the band and thin out toward its
/// edges, and the band's front edge is bent a little, since wind never makes a
/// straight line. Nearer water is lower on the screen, so the band is a bit
/// wider there (perspective).
///
/// Where a dash lands in the sun's golden path, it flashes brighter and warmer.
/// Every dash goes through the OpenWater stencil, so nothing is drawn on the
/// banks or the bridge.
/// </summary>
internal sealed class WindRipple : Happening
{
    private const int Count = 2000;
    private const float BandWidth = 0.30f;            // in U
    private const float Speed = 0.40f;                // in U per second

    private static readonly Color Light = Color.FromArgb(255, 224, 196), Gold = Color.FromArgb(255, 236, 176), Dark = Color.FromArgb(40, 20, 60);

    private readonly DuskScenery _s;
    private readonly float _u, _bandW, _seconds;
    private readonly float _top, _bottom;             // the stretch of water the band sweeps

    private struct Dash { public float Along, Depth, Len, Alpha, Rate, Phase; public bool Pale; }
    private Dash[] _dashes = [];
    private int _dir = 1;

    public override float Seconds => _seconds;
    public override string? Claims => "pond";
    public override int Layer => 1;

    public WindRipple(DuskScenery s)
    {
        _s = s;
        _u = s.U;
        _bandW = BandWidth * _u;
        _seconds = (_s.Width + 1.6f * _bandW) / (Speed * _u) + 0.4f;      // long enough to enter, cross and leave
        _top = s.Horizon + _u * 0.012f;
        _bottom = s.Height - _u * 0.01f;
    }

    public override void Begin(Random rng)
    {
        _dir = rng.Next(2) == 0 ? 1 : -1;
        _dashes = new Dash[Count];
        for (int i = 0; i < Count; i++)
        {
            float depth = (float)Math.Pow(rng.NextDouble(), 1.15);          // a few more dashes on the far water
            // "Along" is the dash's place across the band, -1 (back edge) to +1 (front edge), bunched in the middle:
            // the average of three dice is bell-shaped.
            float along = ((float)rng.NextDouble() + (float)rng.NextDouble() + (float)rng.NextDouble() - 1.5f) / 1.5f;
            _dashes[i] = new Dash
            {
                Along = along,
                Depth = depth,
                Len = _u * (0.005f + 0.015f * (float)rng.NextDouble()) * (0.55f + 0.7f * depth),
                Alpha = 0.2f + 0.2f * (float)rng.NextDouble(),
                Rate = 4f + 6f * (float)rng.NextDouble(),
                Phase = (float)rng.NextDouble() * MathF.Tau,
                Pale = rng.Next(2) == 0,
            };
        }
    }

    public override void Draw(FrameBuffer fb, float t)
    {
        float centre = _dir > 0 ? -0.8f * _bandW + Speed * _u * t : _s.Width + 0.8f * _bandW - Speed * _u * t;
        float thick = Math.Max(1f, _u / 900f);
        float sunX = _s.Sun.At.X, sunR = _s.Sun.R;
        float lake = _bottom - _top;

        foreach (var d in _dashes)
        {
            float y = _top + d.Depth * lake;
            // Wider where nearer. The front edge (toward the direction of travel) bends: the middle leads.
            float half = _bandW * 0.5f * (0.6f + 0.8f * d.Depth);
            float bend = _dir * (0.5f - Math.Abs(d.Depth - 0.5f)) * _bandW * 0.25f;
            float x = centre + d.Along * half + bend;
            if (x < -_u * 0.03f || x > _s.Width + _u * 0.03f) continue;

            // Blink: on about 60 percent of the time, with soft ends.
            float blink = Math.Clamp((float)Math.Sin(t * d.Rate + d.Phase) * 1.6f, 0f, 1f);
            if (blink <= 0.01f) continue;
            float edge = 1f - 0.65f * Math.Abs(d.Along);                    // fainter toward the band's edges
            float alpha = d.Alpha * blink * edge * Fade(t, _seconds, 0.4f, 0.4f);

            Color c = d.Pale ? Light : Dark;
            if (d.Pale)
            {
                // Inside the golden path (it widens toward the viewer, as the painter's does), glints flash brighter and warmer.
                float dd = Math.Clamp((y - _s.Horizon) / (0.8f * (_s.Height - _s.Horizon)), 0f, 1f);
                if (Math.Abs(x - sunX) < sunR * (0.5f + 2.2f * dd)) { c = Gold; alpha = Math.Min(0.95f, alpha * 2.4f); }
            }
            fb.Line(new PointF(x - d.Len / 2, y), new PointF(x + d.Len / 2, y), c, alpha, thick, _s.OpenWater);
        }
    }
}
