using System.Drawing.Drawing2D;
using Peckworks.Screensavers.Core;

namespace Sakura.Happenings;

/// <summary>
/// Cloud shadows: on a breezy day, big soft patches of shade slide slowly
/// across the mountain and the lake, the way they do when you watch a
/// hillside from far away. (You never see the clouds here, only what they
/// do to the ground.)
///
/// How: three soft dark blobs, each in its own "lane" at a different depth.
/// The one on Fuji is a roundish blob. The two on the lake are squashed flat,
/// the farther one flatter still, because ground seen at a low angle is
/// foreshortened (a circle on a road looks like a thin oval). Each blob is a
/// sprite painted once (a few overlapping soft ellipses, so it is lumpy like
/// a cloud's footprint, not a perfect oval) and stamped at low opacity.
///
/// Stencil: the shade only lands on the mountain and on the lake and the far
/// shore, never on the sky and never on the banks, trees or branches in
/// front. So a shadow on Fuji looks like it is ON the mountain, and slips
/// out of sight behind the near bank.
///
/// Cost: the blobs are big, so each lane's sprite is painted a little
/// smaller than the stencil needs and has only soft low opacity; and they
/// never overlap in height, so no pixel is blended twice.
/// </summary>
internal sealed class CloudShadows : Happening
{
    private const int Lanes = 3;

    private readonly Scenery _s;
    private readonly bool[] _shade;                    // where shade may land: the mountain, the lake, the far shore
    private readonly Sprite[] _blob = new Sprite[Lanes];
    private readonly float[] _laneY = new float[Lanes];

    // This showing's dice (rolled in Begin).
    private readonly bool[] _on = new bool[Lanes];
    private readonly float[] _x0 = new float[Lanes], _vx = new float[Lanes], _dy = new float[Lanes];

    public override float Seconds => 18f;
    public override string? Claims => "fuji,costly";               // it shades the mountain, and it is one of the dearest to draw at 4K: sharing "fuji" keeps it from running with Red Fuji or the rainbow, so their costs never add up

    public CloudShadows(Scenery s)
    {
        _s = s;
        float u = s.U, h = s.Height, w = s.Width;
        float hillTop = s.HorizonY - u * 0.06f;
        _shade = new bool[s.FujiMask.Length];
        for (int i = 0; i < _shade.Length; i++)
        {
            int y = i / s.Width;
            _shade[i] = s.FujiMask[i] || (s.OpenBehindBanks[i] && !s.OpenSky[i] && y > hillTop);
        }

        // Lane 0 over the middle of Fuji, lane 1 on the far lake, lane 2 on the near lake.
        _laneY[0] = s.FujiSummit.Y + (s.HorizonY - s.FujiSummit.Y) * 0.45f;
        _laneY[1] = s.HorizonY + u * 0.07f;
        _laneY[2] = Math.Min(h - u * 0.08f, s.HorizonY + u * 0.22f);
        _blob[0] = Blob(u * 0.62f, u * 0.28f);
        _blob[1] = Blob(u * 0.66f, u * 0.12f);
        _blob[2] = Blob(u * 0.66f, u * 0.14f);
    }

    /// <summary>A soft, lumpy patch of shade: three overlapping soft ellipses, opaque in the middle, nothing at the rim.</summary>
    private static Sprite Blob(float width, float height)
    {
        int w = Math.Max(16, (int)width), h = Math.Max(8, (int)height);
        return Sprite.Paint(w, h, g =>
        {
            // (centre x, centre y, width, height) of each lobe, as fractions of the sprite.
            (float X, float Y, float W, float H)[] lobes =
            [
                (0.50f, 0.50f, 0.78f, 0.92f), (0.30f, 0.56f, 0.52f, 0.70f), (0.70f, 0.44f, 0.56f, 0.76f),
            ];
            foreach (var (cx, cy, lw, lh) in lobes)
            {
                float ew = lw * w, eh = lh * h;
                using var path = new GraphicsPath();
                path.AddEllipse(cx * w - ew / 2, cy * h - eh / 2, ew, eh);
                using var brush = new PathGradientBrush(path)
                {
                    CenterColor = Color.FromArgb(255, 24, 36, 74),       // dark blue-grey
                    SurroundColors = [Color.FromArgb(0, 34, 46, 84)],
                    Blend = new Blend { Positions = [0f, 0.2f, 0.5f, 1f], Factors = [0f, 0.55f, 0.92f, 1f] },
                };
                g.FillPath(brush, path);
            }
        });
    }

    public override void Begin(Random rng)
    {
        // Which way the wind blows this time.
        float dir = rng.Next(2) == 0 ? 1f : -1f;
        int skip = rng.Next(4);                            // 0..2 drops that lane (so two patches), 3 keeps all three
        for (int i = 0; i < Lanes; i++)
        {
            _on[i] = i != skip;
            // Each patch crosses near the middle of the screen at some time in the showing.
            float cross = _s.Width * (0.35f + 0.3f * (float)rng.NextDouble());
            _vx[i] = dir * _s.U * (0.06f + 0.02f * (float)rng.NextDouble());
            _x0[i] = cross - _vx[i] * (6f + 6f * (float)rng.NextDouble());   // where it is at t = 0
            _dy[i] = _s.U * 0.03f * ((float)rng.NextDouble() - 0.5f);
        }
    }

    public override void Draw(FrameBuffer fb, float t)
    {
        float env = Fade(t, Seconds, 3.5f, 3.5f);
        if (env < 0.01f) return;
        for (int i = 0; i < Lanes; i++)
        {
            if (!_on[i]) continue;
            float x = _x0[i] + _vx[i] * t;
            float y = _laneY[i] + _dy[i];
            _blob[i].DrawCentered(fb, x, y, (i == 0 ? 0.42f : 0.32f) * env, _shade);   // the middle of a patch is solid in the sprite; the profile is soft, so most of it is lighter than this
        }
    }
}
