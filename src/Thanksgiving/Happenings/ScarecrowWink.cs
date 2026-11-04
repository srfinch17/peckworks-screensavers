using System.Drawing.Drawing2D;
using Peckworks.Screensavers.Core;

namespace Thanksgiving.Happenings;

/// <summary>
/// The scarecrow comes alive for a moment. Its stitched X eyes blink (each X
/// squashes down to a short flat stitch and opens again), it winks one eye at
/// us (the right eye becomes a happy little arch), its stitched smile widens
/// and turns up at the corners while two faint rosy cheeks appear, and then
/// it goes back to being an ordinary scarecrow.
///
/// FEYNMAN VERSION: the scarecrow's face is part of the painted backdrop, so
/// we cannot just "move" it. Instead we do what a sticker shop does: we paint
/// a sticker of the burlap sack's face band, the SAME colour as the sack, and
/// stick it on exactly the same pixels. Everything under the sticker (the
/// old stitches) is covered, and the sticker carries the new face. Because it
/// is the same sack colour right to its edges, nobody can see where it sits.
/// There is one sticker per pose (an eye state for each eye, and how wide the
/// smile is); Draw just picks the right one for the moment.
///
/// "Exactly the same pixels" (rule 11 of the recipe): the sticker's left and
/// top are whole numbers, and everything inside it is painted at (the
/// scarecrow's position minus those whole numbers), so the sticker's pixel
/// grid lines up with the backdrop's.
///
/// The hat overlaps the top of the head and the scarf (stamped live by the
/// scene AFTER layer 0) overlaps the bottom, so the sticker covers only the
/// band in between: from 0.70 up to 0.855 up the scarecrow's height. The
/// sticker's sack shape is pulled in by one pixel so its soft edge never
/// doubles up with the backdrop's soft edge.
/// </summary>
internal sealed class ScarecrowWink : Happening
{
    private const float Total = 6f;

    // Eye states: 0 = the stitched X, 1 = the X squashed half shut, 2 = a flat
    // stitch (blink), 3 = a happy arch (the wink).
    private const int Smiles = 5;                       // how many widths of smile: 0 = as painted, 4 = widest
    private readonly Sprite?[] _pose = new Sprite?[16 * Smiles];   // indexed [left eye * 4 + right eye, smile]
    private readonly int _left, _top;

    public override float Seconds => Total;
    public override int Layer => 0;                     // on the backdrop, under the scarf
    public override string? Claims => "scarecrow";

    public ScarecrowWink(ThanksgivingScenery s)
    {
        float tall = s.ScarecrowHeight;
        float x = s.ScarecrowFoot.X, footY = s.ScarecrowFoot.Y;
        float half = tall * 0.13f;
        float bandTop = footY - tall * 0.855f, bandBottom = footY - tall * 0.70f;
        _left = (int)MathF.Floor(x - half) - 2;
        _top = (int)MathF.Floor(bandTop) - 2;
        int w = (int)MathF.Ceiling(half * 2) + 6, h = (int)MathF.Ceiling(bandBottom - bandTop) + 6;

        // Only the pose pairs the timetable below actually uses are painted.
        (int L, int R)[] pairs = [(0, 0), (1, 1), (2, 2), (0, 1), (0, 3)];
        foreach (var (l, r) in pairs)
            for (int sm = 0; sm < Smiles; sm++)
                _pose[(l * 4 + r) * Smiles + sm] = Sprite.Paint(w, h, g => PaintBand(g, x - _left, footY - _top, tall, bandTop - _top, bandBottom - _top, l, r, sm / (Smiles - 1f)));
    }

    /// <summary>The sack's face band with the eyes in the given states and the smile s wide (0 to 1).</summary>
    private static void PaintBand(Graphics g, float x, float footY, float tall, float clipTop, float clipBottom, int eyeL, int eyeR, float s)
    {
        PointF P(float across, float up) => new(x + tall * across, footY - tall * up);

        // The sack: the SAME ellipse the painter used (0.22 by 0.23 of the height, its top at 0.91 up), pulled in a pixel, clipped to the band.
        GraphicsState before = g.Save();
        g.SetClip(new RectangleF(0, clipTop, x * 2 + tall, clipBottom - clipTop));
        using (var sack = new SolidBrush(Color.FromArgb(204, 168, 110)))
            g.FillEllipse(sack, x - tall * 0.11f + 1f, footY - tall * 0.91f + 1f, tall * 0.22f - 2f, tall * 0.23f - 2f);
        g.Restore(before);

        float pw = Math.Max(1f, tall * 0.012f);
        using var stitch = new Pen(Color.FromArgb(60, 40, 30), pw) { StartCap = LineCap.Round, EndCap = LineCap.Round };

        // Cheeks first, so the stitches lie over them: two soft pink patches that come with the smile.
        if (s > 0.01f)
            using (var blush = new SolidBrush(Color.FromArgb((int)(86 * s), 214, 112, 96)))
                foreach (float side in (float[])[-1, 1])
                {
                    PointF c = P(side * (0.066f + 0.012f * s), 0.772f);
                    g.FillEllipse(blush, c.X - tall * 0.03f, c.Y - tall * 0.017f, tall * 0.06f, tall * 0.034f);
                }

        // The eyes, centred at 0.03 either side of the middle and 0.815 up.
        void Eye(float cx, int state)
        {
            float cy = 0.815f;
            switch (state)
            {
                case 0:
                case 1:
                    float hh = 0.015f * (state == 0 ? 1f : 0.45f), hw = state == 0 ? 0.01f : 0.012f;
                    g.DrawLine(stitch, P(cx - hw, cy + hh), P(cx + hw, cy - hh));
                    g.DrawLine(stitch, P(cx + hw, cy + hh), P(cx - hw, cy - hh));
                    break;
                case 2:                                      // a short flat stitch: the eye shut
                    g.DrawLine(stitch, P(cx - 0.014f, cy), P(cx + 0.014f, cy));
                    break;
                default:                                     // a happy arch, the wink
                    g.DrawBezier(stitch, P(cx - 0.016f, cy - 0.006f), P(cx - 0.008f, cy + 0.016f), P(cx + 0.008f, cy + 0.016f), P(cx + 0.016f, cy - 0.006f));
                    break;
            }
        }
        Eye(-0.03f, eyeL);
        Eye(0.03f, eyeR);

        // The smile: the painter's curve at s = 0, wider and turned up at the corners as s grows.
        float end = 0.05f + 0.026f * s, lift = 0.75f + 0.020f * s;
        float ctl = 0.02f + 0.022f * s, dip = 0.72f - 0.006f * s;
        g.DrawBezier(stitch, P(-end, lift), P(-ctl, dip), P(ctl, dip), P(end, lift));
        // Its three cross stitches, spreading with the smile and following the curve's height.
        foreach (float sx in (float[])[-0.03f, 0f, 0.03f])
        {
            float sxx = sx * (1f + 0.9f * s);
            float k = (sxx / end + 1) / 2;                   // how far along the smile (0 to 1)
            float cy = (1 - k) * (1 - k) * (1 - k) * lift + 3 * (1 - k) * (1 - k) * k * dip + 3 * (1 - k) * k * k * dip + k * k * k * lift;
            g.DrawLine(stitch, P(sxx, cy - 0.010f), P(sxx, cy + 0.010f));
        }
    }

    /// <summary>One smooth 0-to-1 ramp between two moments.</summary>
    private static float Ramp(float t, float a, float b) => Smooth((t - a) / (b - a));

    public override void Draw(FrameBuffer fb, float t)
    {
        // The timetable. Eyes: blink at 0.5, wink 1.7 to 2.7, a slow blink at 3.2; smile widens 1.1 to 1.6, relaxes 3.9 to 4.7.
        int l = 0, r = 0;
        if (t >= 0.5f && t < 0.95f) l = r = t < 0.62f || t >= 0.83f ? 1 : 2;
        else if (t >= 3.2f && t < 3.7f) l = r = t < 3.3f || t >= 3.6f ? 1 : 2;
        else if (t >= 1.7f && t < 2.8f) { l = 0; r = t < 1.78f || t >= 2.68f ? 1 : 3; }

        float smile = Ramp(t, 1.1f, 1.6f) - Ramp(t, 3.9f, 4.7f);
        int sm = Math.Clamp((int)MathF.Round(smile * (Smiles - 1)), 0, Smiles - 1);

        Sprite? p = _pose[(l * 4 + r) * Smiles + sm];
        p?.Draw(fb, _left, _top);
    }
}
