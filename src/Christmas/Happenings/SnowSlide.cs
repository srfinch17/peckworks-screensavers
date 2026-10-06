using System.Drawing.Drawing2D;
using Peckworks.Screensavers.Core;
using Peckworks.Screensavers.Core.Sakura;

namespace Christmas.Happenings;

/// <summary>
/// A clump of snow slides off a pine bough. It sits on the bough's edge,
/// tips slowly outward, drops, breaks into a handful of smaller clumps on
/// the way down, and they land in the snow with a puff of fine snow dust.
/// Then the clumps sink into the snow and fade.
///
/// The bough: a pine's width at any height is known (PineHalfWidth), so the
/// edge of a bough at a chosen height is "trunk x, plus or minus that".
///
/// Falling is plain gravity. A thing that starts still and falls for "a"
/// seconds has dropped 0.5 * g * a * a. The breaking-up works the same way:
/// at break time the main clump's position and speed are noted, and each
/// piece starts from there with its own sideways speed. Where a piece lands
/// is worked out in Begin by asking the ground: "how long until I reach the
/// snow at the x I will be over by then?" (a couple of rounds of guessing
/// settles it).
///
/// Clumps are white with a grey-blue underside so they show on the dark
/// green pine and on the white snow. The sprite of the big clump is painted
/// at 5 tilts, and for both directions (mirrored).
/// </summary>
internal sealed class SnowSlide : Happening
{
    private const int Tilts = 5, MaxPieces = 6, DotsPer = 6, MaxDots = MaxPieces * DotsPer;
    private const float TipTime = 0.6f;       // how long the clump tips before it lets go
    private const float BreakAfter = 0.3f;    // how long after letting go it breaks up

    private readonly ChristmasScenery _s;
    private readonly float _g, _bigR;
    private readonly Sprite[,] _big = new Sprite[2, Tilts];   // [0] tips to the left, [1] tips to the right
    private readonly Sprite[] _small = new Sprite[3];
    private readonly Sprite _dot;

    private float _side, _x0, _y0;                             // which way it tips (-1 left, +1 right) and the clump's starting spot
    private float _xb, _yb, _vyb;                              // where the main clump is, and how fast it is falling, when it breaks
    private int _n;                                            // how many pieces this showing
    private readonly float[] _vx = new float[MaxPieces], _kick = new float[MaxPieces];
    private readonly int[] _kind = new int[MaxPieces];
    private readonly float[] _tf = new float[MaxPieces];       // seconds each piece falls before landing
    private readonly float[] _xl = new float[MaxPieces], _yl = new float[MaxPieces];   // where it lands
    private readonly float[] _dvx = new float[MaxDots], _dvy = new float[MaxDots];

    public override float Seconds => _n == 0 ? 0.2f : 4f;      // no pine to use? end at once

    public SnowSlide(ChristmasScenery s)
    {
        _s = s;
        _g = s.U * 0.5f;
        _bigR = Math.Max(3f, s.U * 0.025f);
        for (int m = 0; m < 2; m++)
            for (int k = 0; k < Tilts; k++)
                _big[m, k] = Clump(_bigR, (m == 0 ? -1 : 1) * 32f * k / (Tilts - 1), true);
        for (int i = 0; i < 3; i++)
            _small[i] = Clump(_bigR * (0.50f - 0.09f * i), 0, false);
        _dot = Sprite.Paint(5, 5, g =>
        {
            using var b = new SolidBrush(Color.FromArgb(235, 244, 255));
            g.FillEllipse(b, 0.5f, 0.5f, 4, 4);
        });
    }

    /// <summary>A lumpy clump of snow: a few overlapping blobs, grey-blue underneath, white on top. "tilt" turns it (degrees).</summary>
    private static Sprite Clump(float r, float tilt, bool lumpy)
    {
        int d = (int)MathF.Ceiling(r * 3.4f) + 4;
        float c = d / 2f;
        // (x, y, radius) of each blob, in units of r.
        (float X, float Y, float R)[] blobs = lumpy
            ? [(0, 0, 1f), (-0.62f, 0.18f, 0.72f), (0.62f, 0.22f, 0.66f), (0.08f, -0.55f, 0.62f)]
            : [(0, 0, 1f), (0.5f, 0.2f, 0.6f)];
        return Sprite.Paint(d, d, g =>
        {
            g.TranslateTransform(c, c);
            g.RotateTransform(tilt);
            using var under = new SolidBrush(Color.FromArgb(255, 158, 180, 222));
            using var top = new SolidBrush(Color.FromArgb(255, 252, 253, 255));
            using var rim = new Pen(Color.FromArgb(120, 130, 154, 206), Math.Max(1f, r * 0.05f));
            foreach (var b in blobs)
            {
                float rr = b.R * r * (lumpy ? 1f : 0.9f);
                g.FillEllipse(under, b.X * r - rr, b.Y * r - rr + r * 0.16f, rr * 2, rr * 2);   // the shaded underside, a bit lower
                g.DrawEllipse(rim, b.X * r - rr, b.Y * r - rr + r * 0.16f, rr * 2, rr * 2);
            }
            foreach (var b in blobs)
            {
                float rr = b.R * r * (lumpy ? 1f : 0.9f);
                g.FillEllipse(top, b.X * r - rr, b.Y * r - rr, rr * 2, rr * 2);                  // the white body on top
            }
        });
    }

    public override void Begin(Random rng)
    {
        _n = 0;
        if (_s.Pines.Count == 0) return;
        var pine = _s.Pines[rng.Next(_s.Pines.Count)];
        float up = 0.30f + 0.40f * (float)rng.NextDouble();             // how far up the tree, 0.3 to 0.7
        float y = pine.Foot - (pine.Foot - pine.Top) * up;
        _side = rng.Next(2) == 0 ? -1 : 1;
        float hw = Brushwork.PineHalfWidth(y, pine.Top, pine.Foot, _s.U);
        _x0 = pine.X + _side * hw * 0.70f;                              // well inside the painted edge (the painted boughs are a bit narrower than the formula), so it sits ON the bough
        _y0 = y;

        // The main clump lets go after TipTime, and falls from there.
        float vxMain = _side * _s.U * 0.02f;
        float a = BreakAfter;
        _xb = _x0 + _side * _bigR * 0.4f + vxMain * a;
        _vyb = _g * a;
        _yb = _y0 + 0.5f * _g * a * a;

        _n = 4 + rng.Next(3);                                           // 4 to 6 pieces
        for (int i = 0; i < _n; i++)
        {
            _kind[i] = rng.Next(3);
            _vx[i] = _s.U * 0.055f * (_side * 0.6f + (float)rng.NextDouble() * 1.6f - 0.8f);   // spread, leaning outward
            _kick[i] = _s.U * 0.02f * (float)rng.NextDouble();                                  // some hop up a little
            float rr = _small[_kind[i]].Width / 3.4f;
            float vy = _vyb - _kick[i];
            // Guess the landing: fall time to the snow at the x we will be over, repeated until it settles.
            float tf = 0.4f, x = _xb;
            for (int k = 0; k < 4; k++)
            {
                x = Math.Clamp(_xb + _vx[i] * tf, 0, _s.Width - 1);
                float d = _s.Ground.YAt(x) - rr * 0.6f - _yb;
                tf = d <= 0 ? 0.05f : (-vy + MathF.Sqrt(vy * vy + 2 * _g * d)) / _g;
            }
            _tf[i] = tf;
            _xl[i] = x;
            _yl[i] = _s.Ground.YAt(x) - rr * 0.6f;
            for (int j = 0; j < DotsPer; j++)
            {
                _dvx[i * DotsPer + j] = _s.U * 0.035f * ((float)rng.NextDouble() * 2 - 1);
                _dvy[i * DotsPer + j] = _s.U * (0.015f + 0.035f * (float)rng.NextDouble());
            }
        }
    }

    public override void Draw(FrameBuffer fb, float t)
    {
        if (_n == 0) return;
        float u = _s.U;
        float sink = Smooth(Seconds - t);                               // 1 = fully there, 0 = sunk and gone (the last second)
        int m = _side < 0 ? 0 : 1;

        if (t < TipTime + BreakAfter)
        {
            float x, y, tilt;
            if (t < TipTime)
            {
                tilt = Smooth(t / TipTime);                              // tips slowly outward
                x = _x0 + _side * _bigR * 0.4f * tilt;
                y = _y0;
            }
            else
            {
                float a = t - TipTime;                                   // let go: gravity takes it
                tilt = 1;
                x = _x0 + _side * _bigR * 0.4f + _side * u * 0.02f * a;
                y = _y0 + 0.5f * _g * a * a;
            }
            // Fade in as the snow "forms" on the bough: it is white on white-ish snow at first.
            _big[m, Math.Min(Tilts - 1, (int)(tilt * Tilts))].DrawCentered(fb, x, y - _bigR * 0.3f, Math.Min(1f, t / 0.15f));
        }

        float tb = TipTime + BreakAfter;
        for (int i = 0; i < _n; i++)
        {
            float a = t - tb;
            if (a < 0) continue;
            Sprite sp = _small[_kind[i]];
            float x, y, op = 1;
            if (a < _tf[i])
            {
                x = _xb + _vx[i] * a;
                y = _yb + (_vyb - _kick[i]) * a + 0.5f * _g * a * a;
            }
            else
            {
                x = _xl[i];
                y = _yl[i];
            }
            // The last second: it sinks into the snow and fades.
            y += (1 - sink) * sp.Height * 0.25f;
            op = sink;
            sp.DrawCentered(fb, x, y, op);

            // The puff of fine snow dust at the landing: dots thrown up that fall back and fade.
            float b = a - _tf[i];
            if (b >= 0 && b < 0.6f)
                for (int j = 0; j < DotsPer; j++)
                {
                    int n = i * DotsPer + j;
                    float dx = _xl[i] + _dvx[n] * b;
                    float dy = _yl[i] + sp.Height * 0.3f - _dvy[n] * b + 0.5f * u * 0.12f * b * b;
                    _dot.DrawCentered(fb, dx, dy, (1 - b / 0.6f) * sink * 0.95f);
                }
        }
    }
}
