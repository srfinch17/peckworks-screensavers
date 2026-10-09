using System.Drawing.Drawing2D;
namespace Peckworks.Screensavers.Core;

/// <summary>
/// Smoke from a chimney (Cabin by Stream's cottage, or a photo's chimney).
///
/// FEYNMAN VERSION: smoke is a crowd of little puffs. Each puff is born at
/// the chimney top, rises fast while it is hot, slows as it cools, spreads
/// out as it rises (it gets bigger and fainter), leans the way the breeze
/// blows, and wanders a little from side to side on the way. After seven or
/// eight seconds it has thinned to nothing, and its slot is reused for a new
/// puff. Several puffs a second come out, so the plume is a continuous
/// column that is dense at the chimney and ragged at the top.
///
/// Each puff is drawn as a soft white disc (a sprite from Core/Sprite.cs).
/// A sprite cannot be scaled when it is stamped, so eight sizes are painted
/// once at startup and each puff picks the one nearest its size right now.
/// </summary>
public sealed class ChimneySmoke
{
    private struct Puff
    {
        public float X, Y, Age, Life, Vx, Vy, Phase;
    }

    private const int Max = 64;
    private const int Sizes = 8;

    private readonly Puff[] _puffs = new Puff[Max];
    private readonly Sprite[] _discs = new Sprite[Sizes];
    private readonly PointF _at;
    private readonly float _u, _amount, _breeze, _maxR;
    private readonly Random _rng;
    private float _emit;      // puffs owed but not yet born (fractions carry over between frames)
    private int _alive;

    /// <param name="amount">1 = normal, 0 = the fire is out, 2 = a roaring fire.</param>
    /// <param name="breeze">1 = a light breeze leaning the plume to the right, 0 = still air.</param>
    /// <param name="color">The smoke's colour. Left out: white with a hint of blue-grey, as
    /// in shade or at dusk. Smoke lit by a low sun takes the sun's warmth instead.</param>
    public ChimneySmoke(PointF chimneyTop, float u, float amount, float breeze, Random rng, Color? color = null)
    {
        Color c = color ?? Color.FromArgb(236, 238, 244);
        _at = chimneyTop; _u = u; _amount = amount; _breeze = breeze; _rng = rng;
        _maxR = u * 0.040f;
        // The eight discs, smallest to largest. No hot centre as a glow has:
        // a plain soft disc, white with a hint of blue-grey.
        for (int i = 0; i < Sizes; i++)
        {
            int r = Math.Max(2, (int)(_maxR * (i + 1) / Sizes));
            _discs[i] = Sprite.Paint(r * 2, r * 2, g =>
            {
                using var path = new GraphicsPath();
                path.AddEllipse(0, 0, r * 2, r * 2);
                using var soft = new PathGradientBrush(path)
                {
                    CenterColor = Color.FromArgb(255, c),
                    SurroundColors = [Color.FromArgb(0, c)],
                    Blend = Sprite.SoftFalloff,
                };
                g.FillPath(soft, path);
            });
        }
    }

    public void Update(float dt)
    {
        if (_amount <= 0) return;
        // Age every puff; a puff past its life is dead and its slot is free.
        for (int i = 0; i < _alive; i++)
        {
            ref Puff p = ref _puffs[i];
            p.Age += dt;
            if (p.Age >= p.Life)
            {
                _puffs[i] = _puffs[--_alive];          // move the last live puff into this slot
                i--;
                continue;
            }
            // Cooling: the rise eases toward a slow drift. The breeze pushes
            // sideways more the higher the puff is (it clears the roof's shelter).
            p.Vy += (-_u * 0.018f - p.Vy) * MathF.Min(1f, 0.35f * dt);
            float lift = (_at.Y - p.Y) / _u;                                // how far above the chimney, in u
            p.Vx += (_breeze * _u * 0.045f * MathF.Min(1f, lift * 3f) - p.Vx) * MathF.Min(1f, 0.5f * dt);
            p.X += (p.Vx + _u * 0.006f * MathF.Sin(p.Age * 1.7f + p.Phase)) * dt;
            p.Y += p.Vy * dt;
        }

        // Birth: about nine a second at normal amount.
        _emit += 9f * _amount * dt;
        while (_emit >= 1f && _alive < Max)
        {
            _emit -= 1f;
            _puffs[_alive++] = new Puff
            {
                X = _at.X + _u * 0.006f * ((float)_rng.NextDouble() - 0.5f),
                Y = _at.Y,
                Age = 0,
                Life = 6.5f + 2f * (float)_rng.NextDouble(),
                Vx = _u * 0.01f * ((float)_rng.NextDouble() - 0.5f),
                Vy = -_u * (0.050f + 0.025f * (float)_rng.NextDouble()),
                Phase = (float)(_rng.NextDouble() * Math.PI * 2),
            };
        }
        if (_emit > 2f) _emit = 2f;                    // the plume is full: do not bank up a burst for later
    }

    /// <param name="opacity">1 = as normal; less to dim the whole plume (smoke in fading light).</param>
    public void Draw(FrameBuffer fb, float opacity = 1f)
    {
        // Oldest first: the top of the plume is painted under the fresher
        // puffs lower down, which overlap it where the column is dense.
        for (int i = _alive - 1; i >= 0; i--)
        {
            ref readonly Puff p = ref _puffs[i];
            float q = p.Age / p.Life;                                       // 0 new ... 1 gone
            float radius = _u * 0.006f + (_maxR - _u * 0.006f) * MathF.Sqrt(q);
            int size = Math.Clamp((int)(radius / _maxR * Sizes) , 0, Sizes - 1);
            float alpha = 0.40f * MathF.Pow(1 - q, 1.5f) * MathF.Min(1f, p.Age * 4f) * MathF.Min(1f, _amount) * opacity;
            if (alpha < 0.01f) continue;
            _discs[size].DrawCentered(fb, MathF.Round(p.X), MathF.Round(p.Y), alpha);
        }
    }
}
