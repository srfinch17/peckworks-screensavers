using System.Drawing.Drawing2D;

namespace Peckworks.Screensavers.Core.Photo;

/// <summary>
/// Soft banks of mist drifting slowly sideways across part of a photo.
///
/// FEYNMAN VERSION: each bank is one long, flat, soft-edged smudge of pale
/// colour (a sprite, painted once), slid a little to the right every frame.
/// When one drifts off the right of its area, a new one comes in from the
/// left at a new height, size and thickness, so the mist never settles into
/// a pattern. A stencil says where mist may show at all: Mountain Cabin's
/// keeps it in the pines; Cotswold Brook's is made from the depth map, so the
/// mist lies over the far water and the near reeds stand crisp in front of it.
///
/// Banks alone read as separate streaks over water. Mist lying on water is a
/// SHEET with the banks only stirring it; the sheet never moves, so it is
/// painted into the photo once (PhotoEvening.LayMist) and costs nothing per
/// frame.
/// </summary>
public sealed class PhotoMist
{
    private struct Bank
    {
        public float X, Y, Speed;
        public int Size;
        public float Strength;
    }

    private readonly Sprite[] _sprites = new Sprite[3];
    private readonly Bank[] _banks;
    private readonly RectangleF _box;          // the screen rectangle the banks drift through
    private readonly bool[] _onlyWhere;
    private readonly float _p;
    private readonly Random _rng;

    /// <param name="area">Where the banks drift, as a box on the photo (left, top, width, height, fractions).</param>
    /// <param name="onlyWhere">Where on the screen mist may show (one true/false per pixel).</param>
    /// <param name="amount">1 = six banks, 0 = none.</param>
    /// <param name="size">1 = Mountain Cabin's banks; 2 = twice as long and as thick.</param>
    /// <param name="stretch">How much longer than that, for the same thickness: mist lying on water is long and thin.</param>
    public PhotoMist(PhotoBackdrop photo, RectangleF area, bool[] onlyWhere, float amount, Random rng,
        Color colour, float size = 1f, float stretch = 1f)
    {
        _p = photo.P;
        _rng = rng;
        _onlyWhere = onlyWhere;
        PointF a = photo.ToScreen(area.Left, area.Top), b = photo.ToScreen(area.Right, area.Bottom);
        _box = new RectangleF(a.X, a.Y, b.X - a.X, b.Y - a.Y);
        for (int s = 0; s < 3; s++)
            _sprites[s] = PaintBank((int)(_p * (0.04f + 0.025f * s) * size * stretch), (int)(_p * (0.010f + 0.005f * s) * size), colour);
        _banks = new Bank[Math.Max(0, (int)(6 * amount))];
        for (int i = 0; i < _banks.Length; i++)
            _banks[i] = NewBank(_box.X + _box.Width * (float)rng.NextDouble());
    }

    /// <summary>A bank starting at x: somewhere up and down the area, with its own size, speed and strength.</summary>
    private Bank NewBank(float x) => new()
    {
        X = x,
        Y = _box.Y + _box.Height * (float)_rng.NextDouble(),
        Speed = _p * (0.004f + 0.006f * (float)_rng.NextDouble()),   // a slow drift, pixels per second
        Size = _rng.Next(3),
        Strength = 0.10f + 0.12f * (float)_rng.NextDouble(),
    };

    public void Update(double dt)
    {
        for (int i = 0; i < _banks.Length; i++)
        {
            ref Bank b = ref _banks[i];
            b.X += b.Speed * (float)dt;
            // Drifted off the right: a new bank comes in from the left.
            if (b.X - _sprites[b.Size].Width / 2f > _box.Right)
                b = NewBank(_box.X - _sprites[2].Width / 2f);
        }
    }

    /// <param name="light">1 = as painted; less = fainter (less light to show it).</param>
    public void Draw(FrameBuffer fb, float light)
    {
        foreach (ref readonly Bank b in _banks.AsSpan())
            _sprites[b.Size].DrawCentered(fb, MathF.Round(b.X), MathF.Round(b.Y), b.Strength * light, _onlyWhere);
    }

    /// <summary>A long, flat, soft-edged patch of pale colour: one bank of mist.</summary>
    private static Sprite PaintBank(int halfW, int halfH, Color colour)
    {
        halfW = Math.Max(4, halfW);
        halfH = Math.Max(2, halfH);
        return Sprite.Paint(halfW * 2, halfH * 2, g =>
        {
            using var path = new GraphicsPath();
            path.AddEllipse(0, 0, halfW * 2, halfH * 2);
            using var soft = new PathGradientBrush(path)
            {
                CenterColor = Color.FromArgb(255, colour),
                SurroundColors = [Color.FromArgb(0, colour)],
                Blend = Sprite.SoftFalloff,
            };
            g.FillPath(soft, path);
        });
    }
}
