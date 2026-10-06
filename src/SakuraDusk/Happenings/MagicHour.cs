using System.Drawing.Drawing2D;
using System.Numerics;
using Peckworks.Screensavers.Core;

namespace SakuraDusk.Happenings;

/// <summary>
/// Magic hour: for a little while the light deepens. The upper sky turns a
/// richer magenta-violet, the sky near the horizon a hotter orange, and the
/// sun's glow swells a touch. Then it settles back. It is subtle: you notice
/// it only if you are watching.
///
/// FEYNMAN VERSION: think of two sheets of colored cellophane held up to the
/// sky, a violet one at the top and an orange one at the bottom, each fading
/// out toward the middle, and you slowly slide them from "not there" to "a
/// little there" and back. That is all this is: two see-through washes laid
/// over the sky, plus a bigger soft glow around the sun.
///
/// HOW IT IS DRAWN: a wash that covers the whole sky is a huge sprite
/// (about 6 million pixels on a 4K screen). Instead of keeping that, we work
/// out each wash's strength ONCE per row of the picture (and, for the orange,
/// once per column, because it is strongest near the sun) in the constructor,
/// then each frame we walk the sky and blend. Every row has one violet
/// strength and every row/column pair one orange strength, so a pixel costs a
/// couple of multiplies. Only open sky is touched (the stencil), so the hills,
/// the pagoda and the branches stay exactly as they were.
/// </summary>
internal sealed class MagicHour : Happening
{
    private const float Peak = 0.22f;                             // the strongest a wash ever gets (0 to 1)
    private readonly DuskScenery _s;
    private readonly int[] _violetRow;                            // violet strength (0..256) for each row
    private readonly int[] _orangeRow;                            // orange strength for each row
    private readonly Sprite _sunGlow;
    private readonly int _rows;
    private readonly int[] _runStart, _runX0, _runX1;           // for each row, its stretches of open sky: runs _runStart[y] up to _runStart[y+1] hold start and end x

    public override float Seconds => 16f;
    // "sun": it swells the sun's glow, so it never runs with the green flash or
    // the god rays. Those two and this are also the dearest to draw on a 4K
    // screen, and sharing a claim ("never at once") keeps their costs from
    // ever adding up in one frame.
    public override string? Claims => "sun,costly";                      // it swells the sun's glow, so it never runs with the green flash or the rays

    public MagicHour(DuskScenery s)
    {
        _s = s;
        _rows = Math.Min(s.Height, (int)s.Horizon + 1);
        float hz = Math.Max(1f, s.Horizon);
        _violetRow = new int[_rows];
        _orangeRow = new int[_rows];
        for (int y = 0; y < _rows; y++)
        {
            float f = y / hz;
            float v = Math.Max(0f, 1f - f / 0.7f);                // strongest at the very top, gone by 70% of the way down
            float o = Smooth((f - 0.35f) / 0.65f);                // starts at 35% down, strongest at the horizon
            _violetRow[y] = (int)(256 * v * (0.6f + 0.4f * v));
            _orangeRow[y] = (int)(256 * o);
        }
        // Find each row's stretches of open sky once, so the frame loop never has to ask the stencil pixel by pixel.
        var starts = new int[_rows + 1]; var x0s = new List<int>(); var x1s = new List<int>();
        for (int y = 0; y < _rows; y++)
        {
            starts[y] = x0s.Count;
            for (int x = 0; x < s.Width; x++)
            {
                if (!s.OpenSky[y * s.Width + x]) continue;
                int from = x;
                while (x < s.Width && s.OpenSky[y * s.Width + x]) x++;
                x0s.Add(from); x1s.Add(x);
            }
        }
        starts[_rows] = x0s.Count;
        _runStart = starts; _runX0 = x0s.ToArray(); _runX1 = x1s.ToArray();
        // A plain soft halo (Sprite.Glow has a near-white hot spot that would show as a speck on the sun's disc).
        int gr = Math.Max(6, (int)(s.Sun.R * 2.1f));
        _sunGlow = Sprite.Paint(gr * 2, gr * 2, g =>
        {
            using var path = new GraphicsPath();
            path.AddEllipse(0, 0, gr * 2, gr * 2);
            using var b = new PathGradientBrush(path)
            {
                CenterColor = Color.FromArgb(200, 255, 170, 90),
                SurroundColors = [Color.FromArgb(0, 255, 170, 90)],
                Blend = Sprite.SoftFalloff,
            };
            g.FillPath(b, path);
        });
    }

    public override void Draw(FrameBuffer fb, float t)
    {
        float k = Fade(t, Seconds, 6f, 6f);
        int strength = (int)(256 * Peak * k);
        if (strength <= 0) return;

        uint[] px = fb.Pixels;
        int vc = Vector<uint>.Count;
        var mRB = new Vector<uint>(0xFF00FF);
        var mG = new Vector<uint>(0x00FF00);
        for (int y = 0; y < Math.Min(_rows, fb.Height); y++)
        {
            // Both washes are the same all the way along a row, so fold the
            // violet and the orange into ONE see-through color for the row:
            // "violet over the sky, then orange over that" equals a single
            // wash of strength A and color C. Then each pixel is one blend.
            float a1 = (_violetRow[y] * strength >> 8) / 256f, a2 = (_orangeRow[y] * strength >> 8) / 256f;
            float aTot = 1 - (1 - a1) * (1 - a2);
            if (aTot <= 0.002f) continue;
            float cr = (150 * a1 * (1 - a2) + 255 * a2) / aTot, cg = (40 * a1 * (1 - a2) + 120 * a2) / aTot, cb = (170 * a1 * (1 - a2) + 30 * a2) / aTot;
            uint wash = ((uint)cr << 16) | ((uint)cg << 8) | (uint)cb;
            uint A = (uint)(aTot * 256), inv = 256 - A;
            uint washRB = (wash & 0xFF00FF) * A, washG = (wash & 0x00FF00) * A;     // the wash's share, worked out once per row
            var vInv = new Vector<uint>(inv);
            var vRB = new Vector<uint>(washRB);
            var vG = new Vector<uint>(washG);
            int row = y * fb.Width;
            // Walk the open-sky runs of this row (found once, in the constructor).
            for (int run = _runStart[y]; run < _runStart[y + 1]; run++)
            {
                int x = _runX0[run], end = Math.Min(_runX1[run], fb.Width);
                // Blend a whole handful of pixels at once (SIMD: one instruction, many pixels).
                // Red and blue share one multiply, green gets another (a classic speed trick).
                for (; x + vc <= end; x += vc)
                {
                    var v = new Vector<uint>(px, row + x);
                    var rb = Vector.ShiftRightLogical((v & mRB) * vInv + vRB, 8) & mRB;
                    var gg = Vector.ShiftRightLogical((v & mG) * vInv + vG, 8) & mG;
                    (rb | gg).CopyTo(px, row + x);
                }
                for (; x < end; x++)                              // the last few that did not fill a handful
                {
                    uint bg = px[row + x];
                    px[row + x] = ((((bg & 0xFF00FF) * inv + washRB) >> 8) & 0xFF00FF) | ((((bg & 0x00FF00) * inv + washG) >> 8) & 0x00FF00);
                }
            }
        }
        // The sun's glow swells a little while the wash is up.
        PointF c = _s.Sun.At;
        _sunGlow.DrawCentered(fb, c.X, c.Y, 0.55f * k, _s.OpenSky);
    }
}
