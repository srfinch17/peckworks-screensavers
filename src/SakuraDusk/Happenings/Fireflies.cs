using Peckworks.Screensavers.Core;

namespace SakuraDusk.Happenings;

/// <summary>
/// Hotaru, fireflies: on summer evenings in Japan they rise from the grass
/// along a stream or pond and wander over the water, each blinking on its
/// own slow beat. Twenty to thirty-five tiny yellow-green lights lift off the
/// two banks and drift out over the pond on slow, curly paths. Some are
/// mirrored faintly in the water.
///
/// HOW A FIREFLY MOVES. Nothing is tracked from frame to frame: where a
/// firefly is at time t comes from a formula. A slow steady drift (up and out
/// over the pond) plus two gentle sine waves added on top, one for sideways
/// and one for up and down, each at its own speed. Add two waves of
/// different speeds and you get a wander that never quite repeats: like a
/// toy on a spring that is itself hanging from a swinging arm.
///
/// HOW IT BLINKS. Each firefly has its own cycle of 2.4 to 4.6 seconds. For
/// about a second of it the light swells up and fades back down (a sine wave
/// squared, so it eases in and out), and for the rest it is dark. All their
/// cycles start at different moments, so the swarm twinkles at random.
///
/// THE REFLECTION. A firefly hovering above still water is mirrored in it:
/// the mirror image is as far BELOW the surface as the firefly is above. We
/// do not know exactly where the surface is under each one, so each firefly
/// is given a hover height, and its reflection is drawn that far again below
/// it. Only on pixels that are open pond water (the OpenWater stencil), so
/// nothing is mirrored onto a bank, the bridge or a tree.
/// </summary>
internal sealed class Fireflies : Happening
{
    private const int Max = 35;
    private static readonly Color Lime = Color.FromArgb(190, 255, 120);

    private readonly DuskScenery _s;
    private readonly Sprite _glow, _halo;

    // This showing's dice (rolled in Begin). Arrays, because there are many flies.
    private int _count;
    private readonly float[] _x0 = new float[Max], _y0 = new float[Max];        // where each rises from
    private readonly float[] _dx = new float[Max], _rise = new float[Max];     // how far it drifts out and up over the whole showing
    private readonly float[] _ax1 = new float[Max], _wx1 = new float[Max], _px1 = new float[Max];   // sideways wander, wave 1
    private readonly float[] _ax2 = new float[Max], _wx2 = new float[Max], _px2 = new float[Max];   // sideways wander, wave 2
    private readonly float[] _ay1 = new float[Max], _wy1 = new float[Max], _py1 = new float[Max];   // up-and-down wander, wave 1
    private readonly float[] _ay2 = new float[Max], _wy2 = new float[Max], _py2 = new float[Max];   // up-and-down wander, wave 2
    private readonly float[] _period = new float[Max], _on = new float[Max], _offset = new float[Max];  // the blink
    private readonly float[] _hover = new float[Max];                          // height above the water, for the reflection

    public override float Seconds => 16f;
    public override int Layer => 1;       // in front of the banks and the trees, behind the falling petals

    public Fireflies(DuskScenery s)
    {
        _s = s;
        int r = Math.Max(4, (int)(s.U * 0.014f));              // pixel floor: 4, so a light still shows in the preview box
        _glow = Sprite.Glow(r, Lime);
        _halo = Sprite.Glow(Math.Max(9, (int)(s.U * 0.04f)), Lime);
    }

    public override void Begin(Random rng)
    {
        float u = _s.U, w = _s.Width;
        _count = rng.Next(20, Max + 1);
        float R() => (float)rng.NextDouble();
        for (int i = 0; i < _count; i++)
        {
            bool left = rng.Next(2) == 0;
            // Left bank: the left third. Right bank: the right third. Each rises from the grass at its spot.
            float x = left ? w * (0.02f + 0.32f * R()) : w * (0.66f + 0.32f * R());
            _x0[i] = x;
            _y0[i] = Math.Min(_s.Height * 0.97f, _s.WalkY(x) + u * 0.10f * R());
            // Drift toward the pond; the ones that start nearest the water travel furthest.
            float toward = left ? 1 : -1;
            _dx[i] = toward * u * (0.06f + 0.36f * R());
            _rise[i] = u * (0.05f + 0.15f * R());
            _ax1[i] = u * (0.010f + 0.020f * R()); _wx1[i] = 0.5f + 0.7f * R(); _px1[i] = R() * MathF.Tau;
            _ax2[i] = u * (0.004f + 0.010f * R()); _wx2[i] = 1.3f + 1.2f * R(); _px2[i] = R() * MathF.Tau;
            _ay1[i] = u * (0.008f + 0.016f * R()); _wy1[i] = 0.4f + 0.7f * R(); _py1[i] = R() * MathF.Tau;
            _ay2[i] = u * (0.003f + 0.008f * R()); _wy2[i] = 1.4f + 1.4f * R(); _py2[i] = R() * MathF.Tau;
            _period[i] = 2.2f + 1.8f * R();
            _on[i] = 1.0f + 0.5f * R();
            _offset[i] = R() * _period[i];
            _hover[i] = u * (0.02f + 0.03f * R());
        }
    }

    public override void Draw(FrameBuffer fb, float t)
    {
        float swarm = Fade(t, Seconds, 2.2f, 2.2f);        // the whole swarm fades in and out
        float q = Smooth(t / Seconds);                      // 0 to 1 over the showing: the drift, easing at both ends
        int w = _s.Width, h = _s.Height;
        float waterTop = _s.Horizon + _s.U * 0.04f;

        for (int i = 0; i < _count; i++)
        {
            // The blink: a smooth swell for _on seconds of each cycle, dark the rest.
            float ph = (t + _offset[i]) % _period[i];
            if (ph >= _on[i]) continue;
            float s = MathF.Sin(MathF.PI * ph / _on[i]);
            float b = s * s * swarm;
            if (b < 0.02f) continue;

            float x = _x0[i] + _dx[i] * q
                    + _ax1[i] * MathF.Sin(t * _wx1[i] + _px1[i]) + _ax2[i] * MathF.Sin(t * _wx2[i] + _px2[i]);
            float y = _y0[i] - _rise[i] * q
                    + _ay1[i] * MathF.Sin(t * _wy1[i] + _py1[i]) + _ay2[i] * MathF.Sin(t * _wy2[i] + _py2[i]);
            x = MathF.Round(x);
            y = MathF.Round(y);
            if (x < 0 || x >= w || y < 0 || y >= h) continue;

            // The reflection first (underneath the light), only if this one is out over the open pond.
            if (y > waterTop && _s.OpenWater[(int)y * w + (int)x])
            {
                float ry = y + 2 * _hover[i];
                _glow.DrawCentered(fb, x + MathF.Sin(t * 3f + _px1[i]), ry, 0.28f * b, _s.OpenWater);
            }

            _halo.DrawCentered(fb, x, y, 0.55f * b);
            _glow.DrawCentered(fb, x, y, b);
        }
    }
}
