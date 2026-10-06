using System.Drawing.Drawing2D;
using Peckworks.Screensavers.Core;

namespace SakuraDusk.Happenings;

/// <summary>
/// Evening mist gathers over the pond: four to six long, low, very soft bands
/// of pale lavender-pink drift across the water at different depths and
/// speeds, slowly changing shape, then thin away.
///
/// FEYNMAN VERSION: think of breath on a cold window, but sideways. A band of
/// mist is three or four soft ovals laid end to end and overlapping. Each oval
/// is a blurry blob (brightest in the middle, fading to nothing at the edge)
/// that we paint ONCE as a sticker. Every frame each oval slides a little to
/// the left or right and bobs up and down on its own slow wave, so the band
/// seems to breathe and change its outline, never repeating.
///
/// COST: big soft stickers are the dearest thing in this scene (every pixel of
/// the sticker is looked at, even the see-through ones). So there are only
/// three blob sizes, each oval is thin, and the whole showing has at most 24
/// ovals. Each oval has low opacity; where they overlap, they add up to about
/// a quarter, which is the most the mist is ever allowed.
///
/// The mist goes through the OpenWater stencil, so the bridge and the banks
/// stay in front of it.
/// </summary>
internal sealed class PondMist : Happening
{
    private const int Blobs = 3;

    private readonly DuskScenery _s;
    private readonly float _u;
    private readonly Sprite[] _blob = new Sprite[Blobs];

    // One band: where it floats, how it moves, and its ovals' private waves.
    private struct Band
    {
        public float X0, Y, Speed, Scale, Peak;
        public int Count;
        public float[] Along, Rate, PhaseX, PhaseY, PhaseA;
        public int[] Kind;
    }
    private Band[] _bands = [];

    public override float Seconds => 18f;
    public override string? Claims => null;
    public override int Layer => 1;

    public PondMist(DuskScenery s)
    {
        _s = s;
        _u = s.U;
        // Three oval sizes (width x height in U): short and plump, middle, long and thin.
        (float W, float H)[] sizes = [(0.20f, 0.030f), (0.28f, 0.036f), (0.36f, 0.042f)];
        for (int i = 0; i < Blobs; i++)
        {
            int w = Math.Max(24, (int)(_u * sizes[i].W)), h = Math.Max(8, (int)(_u * sizes[i].H));
            _blob[i] = Sprite.Paint(w, h, g =>
            {
                using var path = new GraphicsPath();
                path.AddEllipse(0, 0, w, h);
                using var br = new PathGradientBrush(path)
                {
                    CenterColor = Color.FromArgb(255, 248, 222, 242),      // pale lavender-pink
                    SurroundColors = [Color.FromArgb(0, 248, 222, 242)],
                    // Melts away with no visible rim, and is exactly zero over the outer third, which the stamp skips
                    // cheaply (a clear pixel costs almost nothing; a faint one still gets blended).
                    Blend = new Blend { Positions = [0f, 0.3f, 0.55f, 0.8f, 1f], Factors = [0f, 0f, 0.15f, 0.55f, 1f] },
                };
                g.FillPath(br, path);
            });
        }
    }

    public override void Begin(Random rng)
    {
        int n = rng.NextDouble() < 0.25 ? 5 : 4;                            // 4 or 5 bands
        _bands = new Band[n];
        for (int i = 0; i < n; i++)
        {
            // Depth in 0..1 from the far shore down to a little below the bridge's feet.
            float d = (i + 0.2f + 0.6f * (float)rng.NextDouble()) / n;
            float length = _u * (0.4f + 0.4f * (float)rng.NextDouble());
            int cnt = 3;                                                    // three ovals laid end to end, overlapping (each one costs the 4K frame, so no more)
            float spacing = length / (cnt - 1);
            // The smallest blob that still overlaps its neighbour (no gaps in the band).
            int kind = 0;
            while (kind < Blobs - 1 && _blob[kind].Width < spacing * 1.25f) kind++;
            var b = new Band
            {
                Y = _s.Horizon + _u * 0.045f + d * (_s.Bridge.WaterLine - _s.Horizon - _u * 0.045f) * 1.05f,
                Scale = 0.75f + 0.55f * d,                                  // nearer mist is bigger
                Speed = _u * (0.010f + 0.020f * (float)rng.NextDouble()) * (rng.Next(2) == 0 ? 1 : -1),
                Peak = 0.50f + 0.10f * (float)rng.NextDouble(),             // each oval's strength; they overlap to about 0.25
                Count = cnt,
                Along = new float[cnt], Rate = new float[cnt], PhaseX = new float[cnt], PhaseY = new float[cnt], PhaseA = new float[cnt],
                Kind = new int[cnt],
            };
            // Start so it is on screen for most of the showing.
            b.X0 = _s.Width * (0.1f + 0.8f * (float)rng.NextDouble());
            for (int j = 0; j < cnt; j++)
            {
                b.Along[j] = length * (j / (float)(cnt - 1) - 0.5f);
                b.Rate[j] = 0.25f + 0.35f * (float)rng.NextDouble();         // slow: a full change takes 10 to 25 seconds
                b.PhaseX[j] = (float)rng.NextDouble() * MathF.Tau;
                b.PhaseY[j] = (float)rng.NextDouble() * MathF.Tau;
                b.PhaseA[j] = (float)rng.NextDouble() * MathF.Tau;
                b.Kind[j] = kind;
            }
            _bands[i] = b;
        }
    }

    public override void Draw(FrameBuffer fb, float t)
    {
        float on = Fade(t, Seconds, 4f, 5f);
        foreach (var b in _bands)
        {
            float cx = b.X0 + b.Speed * t;
            for (int j = 0; j < b.Count; j++)
            {
                Sprite blob = _blob[b.Kind[j]];
                float wave = t * b.Rate[j];
                // Each oval slides a little along the band and bobs a little up and down, on its own slow waves.
                float x = cx + b.Along[j] + (float)Math.Sin(wave + b.PhaseX[j]) * _u * 0.05f;
                float y = b.Y + (float)Math.Sin(wave * 0.8 + b.PhaseY[j]) * _u * 0.006f;
                float breathe = 0.75f + 0.25f * (float)Math.Sin(wave * 0.7 + b.PhaseA[j]);
                blob.DrawCentered(fb, x, y, b.Peak * breathe * on, _s.OpenWater);
            }
        }
    }
}
