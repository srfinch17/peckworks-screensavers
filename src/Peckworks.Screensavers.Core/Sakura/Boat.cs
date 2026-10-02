using System.Drawing.Drawing2D;

namespace Peckworks.Screensavers.Core.Sakura;

/// <summary>
/// A small Japanese river boat that drifts slowly across the water: a low
/// wooden hull with an upswept bow and stern, a little cabin with paper
/// screen walls and a curved roof, a boatman in a straw hat poling at the
/// stern, and a red paper lantern on a post at the bow. Shared by the sakura
/// screensavers that have water.
///
/// HOW IT WORKS
///
/// The boat is a "sprite" (see Sprite.cs): it is painted once at startup
/// onto a small see-through sheet, and every frame that sheet is stamped
/// onto the picture a little farther along. A second sheet holds the same
/// boat painted upside down; stamped faintly just below the first, it is
/// the boat's reflection.
///
/// The boat has to pass BEHIND anything that stands in front of the water
/// (a bank, a tree, a bridge). The scene's painter hands over a stencil:
/// one true/false per pixel that says "this pixel is still open water (or
/// open sky)". The boat is only stamped where the stencil says true, so
/// the foreground hides it without the boat knowing what the foreground is.
///
/// Where the boat is comes straight from the clock (distance = speed times
/// time), wrapped around so that after it leaves one side it spends a
/// while out of sight and then comes in again from the other.
/// </summary>
public sealed class Boat
{
    // The sprite sheet's size, in boat lengths, and where the waterline sits on it.
    private const float SheetWidth = 1.12f, SheetHeight = 0.52f, Waterline = 0.50f;

    private readonly Sprite _boat, _reflection, _lanternGlow;
    private readonly int _screenWidth;
    private readonly float _waterlineY, _length, _speed, _startOffset;
    private double _time;   // a double, so the boat still moves smoothly after days of running

    /// <param name="screenWidth">The picture's width, so the boat knows when it has left.</param>
    /// <param name="waterlineY">The height on screen where the hull meets the water.</param>
    /// <param name="u">The size unit (see Brushwork): the boat's length and speed scale with it.</param>
    /// <param name="shade">
    /// How far the boat is pushed toward a dark silhouette: 0 = plain
    /// daylight colors, 1 = solid dusk shadow. An evening scene passes
    /// something like 0.6, so the boat sits in the same light as its hills.
    /// </param>
    /// <param name="shadow">The color the boat is shaded toward (the scene's own darkest tone).</param>
    /// <param name="scale">
    /// 1 = a boat in the middle distance. Smaller for a boat farther away:
    /// it is drawn smaller AND crosses the screen more slowly, which is how
    /// distance looks (the parallax idea from the petals).
    /// </param>
    public Boat(int screenWidth, float waterlineY, float u, float shade, Color shadow, Random rng, float scale = 1f)
    {
        _screenWidth = screenWidth;
        _waterlineY = waterlineY;
        _length = u * 0.11f * scale;
        _speed = u * 0.012f * scale;                           // slow: about two and a half minutes to cross a wide screen at scale 1

        int sheetW = (int)(SheetWidth * _length), sheetH = (int)(SheetHeight * _length);
        // Start somewhere in the open middle of the water (30% to 60% of the
        // way across), so the boat is in view from the first frame and not
        // hidden behind the trees at the sides.
        _startOffset = sheetW + screenWidth * (0.30f + 0.30f * (float)rng.NextDouble());
        _boat = Sprite.Paint(sheetW, sheetH, g => PaintBoat(g, _length, shade, shadow));
        // The reflection: the same painting, flipped top to bottom. Scaling
        // y by -1 turns the picture upside down; the shift afterwards slides
        // it back onto the sheet.
        _reflection = Sprite.Paint(sheetW, sheetH, g =>
        {
            g.TranslateTransform(0, sheetH);
            g.ScaleTransform(1, -1);
            PaintBoat(g, _length, shade, shadow);
        });
        _lanternGlow = Sprite.Glow((int)(_length * 0.09f), Color.FromArgb(255, 120, 60));
    }

    /// <summary>Move the clock forward by dt seconds.</summary>
    public void Update(float dt) => _time += dt;

    /// <summary>Stamp the boat and its reflection wherever the stencil says there is open water.</summary>
    public void Draw(FrameBuffer fb, bool[] openWater)
    {
        // One trip = across the screen, plus the boat's own length, plus a
        // stretch out of sight. "%" (remainder) makes the trip repeat.
        double trip = _screenWidth + _boat.Width + _length * 3;
        int left = (int)((_time * _speed + _startOffset) % trip) - _boat.Width;

        // A slow, small rise and fall, as if on a gentle swell.
        float bob = _length * 0.006f * (float)Math.Sin(_time * 0.9);
        int top = (int)(_waterlineY - Waterline * _length + bob);

        // The reflection hangs directly below the waterline. On the flipped
        // sheet the waterline sits (SheetHeight - Waterline) from the top.
        int reflectionTop = (int)(_waterlineY - (SheetHeight - Waterline) * _length - bob);
        _reflection.Draw(fb, left, reflectionTop, 0.26f, openWater);
        _boat.Draw(fb, left, top, 1f, openWater);

        // The lantern's glow, breathing slightly like a real flame.
        float glow = 0.55f + 0.12f * (float)Math.Sin(_time * 6.3) + 0.06f * (float)Math.Sin(_time * 14.1);
        _lanternGlow.DrawCentered(fb, left + 0.90f * _length, top + 0.345f * _length, glow, openWater);
    }

    /// <summary>
    /// Paints the boat, facing right. Every position is (across, down) in
    /// boat lengths, turned into pixels by the helper P. The water is at
    /// 0.50 down; the bow is at the right.
    /// </summary>
    private static void PaintBoat(Graphics g, float length, float shade, Color shadow)
    {
        PointF P(float across, float down) => new(across * length, down * length);
        Color Lit(int r, int gr, int b) => Brushwork.Mix(Color.FromArgb(r, gr, b), shadow, shade);
        Pen Line(Color c, float width) => new(c, Math.Max(1f, width * length)) { StartCap = LineCap.Round, EndCap = LineCap.Round };

        // The pole first, so the boatman's hands and the hull cover its middle.
        using (Pen pole = Line(Lit(150, 120, 84), 0.008f))
            g.DrawLine(pole, P(0.29f, 0.235f), P(0.05f, 0.50f));

        // ---- The hull: long and low, sweeping up at the stern (left) and higher at the bow (right) ----
        using (var hull = new SolidBrush(Lit(104, 66, 42)))
            g.FillClosedCurve(hull, [
                P(0.04f, 0.355f), P(0.13f, 0.425f), P(0.50f, 0.435f), P(0.86f, 0.425f), P(1.03f, 0.33f),
                P(0.95f, 0.455f), P(0.84f, 0.50f), P(0.50f, 0.505f), P(0.17f, 0.50f), P(0.08f, 0.45f)], FillMode.Winding, 0.3f);
        using (Pen gunwale = Line(Lit(166, 118, 74), 0.010f))   // the lighter plank along the top edge
            g.DrawCurve(gunwale, [P(0.06f, 0.37f), P(0.14f, 0.428f), P(0.50f, 0.438f), P(0.86f, 0.428f), P(1.01f, 0.345f)], 0.3f);

        // ---- The cabin: paper screen walls, dark posts, and a roof whose eaves turn up ----
        using (var paper = new SolidBrush(Lit(238, 226, 196)))
            g.FillRectangle(paper, 0.38f * length, 0.335f * length, 0.34f * length, 0.10f * length);
        using (Pen post = Line(Lit(70, 48, 40), 0.008f))
        {
            for (int i = 0; i <= 5; i++)
                g.DrawLine(post, P(0.38f + 0.068f * i, 0.335f), P(0.38f + 0.068f * i, 0.435f));
            g.DrawLine(post, P(0.38f, 0.385f), P(0.72f, 0.385f));
        }
        using (var roof = new SolidBrush(Lit(62, 46, 44)))
            g.FillPolygon(roof, [
                P(0.325f, 0.325f), P(0.35f, 0.34f), P(0.75f, 0.34f), P(0.775f, 0.325f),   // the eaves, tips turned up
                P(0.70f, 0.292f), P(0.40f, 0.292f)]);

        // ---- The boatman at the stern: robe, head, and a wide cone of a straw hat ----
        using (var robe = new SolidBrush(Lit(60, 50, 54)))
            g.FillPolygon(robe, [P(0.185f, 0.43f), P(0.24f, 0.43f), P(0.228f, 0.315f), P(0.197f, 0.315f)]);
        using (var skin = new SolidBrush(Lit(232, 196, 160)))
            g.FillEllipse(skin, 0.200f * length, 0.288f * length, 0.026f * length, 0.026f * length);
        using (var straw = new SolidBrush(Lit(218, 190, 124)))
            g.FillPolygon(straw, [P(0.172f, 0.300f), P(0.213f, 0.262f), P(0.254f, 0.300f)]);

        // ---- The lantern at the bow: a post and a red paper lantern (the scene adds its glow) ----
        using (Pen lanternPost = Line(Lit(70, 48, 40), 0.007f))
            g.DrawLine(lanternPost, P(0.90f, 0.43f), P(0.90f, 0.36f));
        using var lantern = new SolidBrush(Color.FromArgb(236, 84, 48));   // not shaded: it makes its own light
        g.FillEllipse(lantern, 0.887f * length, 0.328f * length, 0.026f * length, 0.034f * length);
    }
}
