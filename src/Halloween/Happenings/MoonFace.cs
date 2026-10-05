using System.Drawing.Drawing2D;
using Peckworks.Screensavers.Core;

namespace Halloween.Happenings;

/// <summary>
/// A face fades into the moon, as if the craters had quietly rearranged
/// themselves, holds for a moment (and does one small living thing), then
/// melts back into the moon.
///
/// The face is painted in the same warm grey-tan as the moon's seas, never
/// at full strength (peak opacity 0.6), with a soft halo around each
/// feature. That is what makes it look like part of the moon and not a
/// sticker laid on top.
///
/// Three faces, picked in Begin: a grinning jack-o'-lantern, a skull, and a
/// startled "ooo" ghost. Each face is made of THREE sprites:
///   - "fixed": the parts that never move (a nose, one eye).
///   - "vary 0" and "vary 1": the one part that does something (a wink, the
///     jaw dropping, the eyes glancing aside). In the middle of the hold we
///     crossfade from 0 to 1 and back. Keeping the moving part separate
///     means the still parts are stamped only once and never get a
///     double-strength dip during the crossfade.
///
/// Where it lands: rule 11 of docs/ADDING_HAPPENINGS.md. The moon was painted
/// at a fractional position, so we pick the sprite's whole-pixel left and
/// top first, then paint the face inside it at the moon's true center minus
/// those. Then the face sits on exactly the pixels of the moon.
/// </summary>
internal sealed class MoonFace : Happening
{
    private const int Faces = 3;
    private static readonly Color Crater = Color.FromArgb(150, 120, 80);   // the same warm grey-tan as the moon's seas

    private readonly int _left, _top;
    private readonly Sprite[] _fixed = new Sprite[Faces];
    private readonly Sprite[][] _vary = new Sprite[Faces][];
    private readonly bool[] _stencil;   // OpenSky, with the moon's faint branch-edge pixels allowed (see BloodMoon.MoonStencil)
    private int _face;

    public override float Seconds => 9f;
    public override string? Claims => "moon";

    public MoonFace(HalloweenScenery s)
    {
        _stencil = BloodMoon.MoonStencil(s);
        float r = s.Moon.R;
        _left = (int)MathF.Floor(s.Moon.At.X - r) - 2;
        _top = (int)MathF.Floor(s.Moon.At.Y - r) - 2;
        int size = (int)MathF.Ceiling(r * 2) + 6;
        float cx = s.Moon.At.X - _left, cy = s.Moon.At.Y - _top;

        // Paints one sprite. Inside, 1 unit = one moon radius and (0,0) is the
        // moon's center, so every number below is "a fraction of the moon".
        Sprite Make(Action<Graphics> draw) => Sprite.Paint(size, size, g =>
        {
            g.TranslateTransform(cx, cy);
            g.ScaleTransform(r, r);
            using var disc = new GraphicsPath();
            disc.AddEllipse(-0.98f, -0.98f, 1.96f, 1.96f);
            g.SetClip(disc, CombineMode.Replace);          // a soft halo can never spill off the moon
            draw(g);
        });

        _fixed[0] = Make(g => { Tri(g, -0.50f, -0.08f, -0.14f, -0.08f, -0.32f, -0.42f); Nose(g); Grin(g); });
        _vary[0] = [Make(g => Tri(g, 0.14f, -0.08f, 0.50f, -0.08f, 0.32f, -0.42f)),                 // eye open
                    Make(g => Line(g, [new(0.12f, -0.12f), new(0.32f, -0.30f), new(0.52f, -0.12f)], 0.07f))]; // eye shut: a wink

        _fixed[1] = Make(g => { Oval(g, -0.30f, -0.14f, 0.19f, 0.21f); Oval(g, 0.30f, -0.14f, 0.19f, 0.21f); Tri(g, 0f, 0.02f, -0.08f, 0.18f, 0.08f, 0.18f); });
        _vary[1] = [Make(g => Teeth(g, 0f)), Make(g => Teeth(g, 0.10f))];                            // jaw shut, then dropped

        // The ghost's eyes are rings with a dark dot in them (the pupil), so a glance reads.
        _fixed[2] = Make(g => { Ring(g, -0.28f, -0.16f); Ring(g, 0.28f, -0.16f); Brows(g); Oval(g, 0f, 0.30f, 0.15f, 0.23f); });
        _vary[2] = [Make(g => { Oval(g, -0.28f, -0.16f, 0.07f, 0.09f); Oval(g, 0.28f, -0.16f, 0.07f, 0.09f); }),
                    Make(g => { Oval(g, -0.28f + 0.075f, -0.16f, 0.07f, 0.09f); Oval(g, 0.28f + 0.075f, -0.16f, 0.07f, 0.09f); })];
    }

    public override void Begin(Random rng) => _face = rng.Next(Faces);

    public override void Draw(FrameBuffer fb, float t)
    {
        // Fade in 2.5 s, hold 4 s, fade out 2.5 s. Never above 0.6, so the moon's own seas still show through.
        float o = 0.6f * Fade(t, Seconds, 2.5f, 2.5f);
        // The one small living thing: from 4.0 s to 6.1 s the moving part crossfades to its second pose and back.
        float a = Smooth((t - 3.9f) / 0.5f) * (1 - Smooth((t - 5.6f) / 0.5f));

        _fixed[_face].Draw(fb, _left, _top, o, _stencil);
        _vary[_face][0].Draw(fb, _left, _top, o * (1 - a), _stencil);
        _vary[_face][1].Draw(fb, _left, _top, o * a, _stencil);
    }

    // ---- The pencils. Each draws one feature with a soft halo: three passes
    //      of a wider, fainter outline, then the solid shape on top. Like
    //      smudging charcoal with a thumb so the edge is not a hard line. ----

    private static void Soft(Graphics g, GraphicsPath p, float strokeWidth)
    {
        (float extra, int alpha)[] halo = [(0.07f, 35), (0.045f, 60), (0.022f, 100)];
        foreach (var (extra, alpha) in halo)
        {
            using var pen = new Pen(Color.FromArgb(alpha, Crater), strokeWidth + extra)
            { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawPath(pen, p);
        }
        if (strokeWidth > 0)
        {
            using var pen = new Pen(Crater, strokeWidth) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawPath(pen, p);
        }
        else
        {
            using var fill = new SolidBrush(Crater);
            g.FillPath(fill, p);
        }
    }

    private static void Poly(Graphics g, PointF[] pts) { using var p = new GraphicsPath(); p.AddPolygon(pts); Soft(g, p, 0); }
    private static void Tri(Graphics g, float x1, float y1, float x2, float y2, float x3, float y3) => Poly(g, [new(x1, y1), new(x2, y2), new(x3, y3)]);
    private static void Oval(Graphics g, float cx, float cy, float rx, float ry) { using var p = new GraphicsPath(); p.AddEllipse(cx - rx, cy - ry, rx * 2, ry * 2); Soft(g, p, 0); }
    private static void Line(Graphics g, PointF[] pts, float width) { using var p = new GraphicsPath(); p.AddLines(pts); Soft(g, p, width); }

    private static void Nose(Graphics g) => Tri(g, 0f, 0.0f, -0.09f, 0.16f, 0.09f, 0.16f);

    /// <summary>The jack-o'-lantern's grin: a wide smile whose lower edge is a row of jagged teeth.</summary>
    private static void Grin(Graphics g) => Poly(g,
    [
        new(-0.58f, 0.22f), new(-0.30f, 0.34f), new(0f, 0.38f), new(0.30f, 0.34f), new(0.58f, 0.22f),
        new(0.46f, 0.52f), new(0.32f, 0.42f), new(0.16f, 0.60f), new(0f, 0.46f),
        new(-0.16f, 0.60f), new(-0.32f, 0.42f), new(-0.46f, 0.52f),
    ]);

    /// <summary>The skull's teeth: a box with tooth gaps. "drop" slides it down to open the jaw.</summary>
    private static void Teeth(Graphics g, float drop)
    {
        float top = 0.38f + drop * 0.3f, bottom = 0.62f + drop;
        Line(g, [new(-0.36f, top), new(0.36f, top), new(0.36f, bottom), new(-0.36f, bottom), new(-0.36f, top)], 0.04f);
        foreach (float x in (float[])[-0.18f, 0f, 0.18f])
            Line(g, [new(x, top), new(x, bottom)], 0.035f);
    }

    /// <summary>A startled ghost eye: a ring of crater-colour (the pupil is a separate sprite so it can glance).</summary>
    private static void Ring(Graphics g, float cx, float cy)
    {
        using var p = new GraphicsPath();
        p.AddEllipse(cx - 0.15f, cy - 0.20f, 0.30f, 0.40f);
        Soft(g, p, 0.04f);
    }

    /// <summary>Two raised eyebrows: the "ooo!" in the face.</summary>
    private static void Brows(Graphics g)
    {
        Line(g, [new(-0.46f, -0.50f), new(-0.30f, -0.56f), new(-0.12f, -0.50f)], 0.04f);
        Line(g, [new(0.12f, -0.50f), new(0.30f, -0.56f), new(0.46f, -0.50f)], 0.04f);
    }
}
