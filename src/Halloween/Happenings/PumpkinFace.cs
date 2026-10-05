using System.Drawing.Drawing2D;
using Peckworks.Screensavers.Core;

namespace Halloween.Happenings;

/// <summary>
/// One jack-o'-lantern changes its expression. Its candle flares, the grin
/// melts into a new face (surprised, sad, wicked, winking or goofy), holds it
/// for a while, flares again, changes to a second face, holds, and finally
/// goes back to the grin it started with.
///
/// How it works: the backdrop already shows every pumpkin with the default
/// grin. For each pumpkin we paint five "replacement" sprites ahead of time,
/// each the same pumpkin body with a different carved face. To change the
/// face we stamp a replacement over the backdrop pumpkin and fade it in. To
/// go back to the grin we just fade the replacement out and the backdrop
/// shows through again. Nothing needs to be "erased".
///
/// The one hard part is that the sprite must land on EXACTLY the same pixels
/// as the backdrop pumpkin, or you would see two outlines (like a badly
/// printed comic where the colors are slightly off). So: we pick the
/// sprite's whole-number left and top first, then paint the pumpkin inside it
/// at the same fractional offset the backdrop had, and stamp with Draw (not
/// DrawCentered, which would round the position a second time).
/// </summary>
internal sealed class PumpkinFace : Happening
{
    private const int Faces = 5;                           // surprised, sad, wicked, winking, goofy
    private static readonly Color Candle = Color.FromArgb(255, 222, 110);   // the same carved-hole yellow as the default face

    private readonly HalloweenScenery _s;
    private readonly Sprite[][] _faces;                    // [pumpkin][face]
    private readonly (int Left, int Top)[] _where;         // each pumpkin sprite's whole-number top left corner
    private readonly Sprite[] _flare;                      // one orange glow per pumpkin
    private int _pumpkin, _a, _b;                          // this showing: which pumpkin, and its two faces

    public override float Seconds => 15f;
    public override string? Claims => "pumpkins";

    // The timeline, in seconds. Each change is a 0.4 s crossfade.
    private const float ToA = 0.5f, ToB = 7.0f, Back = 13.5f, Fading = 0.4f;

    public PumpkinFace(HalloweenScenery s)
    {
        _s = s;
        int n = s.Pumpkins.Count;
        _faces = new Sprite[n][];
        _where = new (int, int)[n];
        _flare = new Sprite[n];

        for (int i = 0; i < n; i++)
        {
            (PointF at, float r) = s.Pumpkins[i];
            float footY = at.Y + 0.8f * r;                 // the backdrop painted the body with its foot here

            // Whole-number left and top first (rule 11). The body reaches
            // 1.0 r either side, 1.1 r up and 0.8 r down; 2 spare pixels all round.
            int left = (int)MathF.Floor(at.X - r) - 2, top = (int)MathF.Floor(at.Y - 1.1f * r) - 2;
            int width = (int)MathF.Ceiling(2 * r) + 6, height = (int)MathF.Ceiling(1.9f * r) + 6;
            _where[i] = (left, top);

            _faces[i] = new Sprite[Faces];
            for (int f = 0; f < Faces; f++)
            {
                int face = f;
                _faces[i][f] = Sprite.Paint(width, height, g =>
                {
                    // Same x and footY as the backdrop, just measured from this sprite's corner.
                    float cy = HalloweenPainter.PaintPumpkinBody(g, at.X - left, footY - top, r).Y;
                    Carve(g, at.X - left, cy, r, face);
                });
            }

            _flare[i] = Sprite.Glow(Math.Max(4, (int)(r * 2.8f)), Color.FromArgb(255, 150, 40));
        }
    }

    public override void Begin(Random rng)
    {
        _pumpkin = rng.Next(_s.Pumpkins.Count);
        _a = rng.Next(Faces);
        _b = (_a + 1 + rng.Next(Faces - 1)) % Faces;       // any face except A
    }

    /// <summary>
    /// Carves one face. Every point is (across, down) from the pumpkin's
    /// center, in units of r, exactly like the default face in
    /// HalloweenPainter.PaintPumpkin. The yellow is the candle shining
    /// through the holes, so a face is just a set of yellow shapes.
    /// </summary>
    private static void Carve(Graphics g, float x, float cy, float r, int face)
    {
        PointF P(float ax, float ay) => new(x + r * ax, cy + r * ay);
        RectangleF Disc(float ax, float ay, float rad) => new(x + r * (ax - rad), cy + r * (ay - rad), r * rad * 2, r * rad * 2);
        // A carved line (a brow, a smile): a yellow stroke with round ends, at least 1 pixel thick.
        using var stroke = new Pen(Candle, Math.Max(1f, r * 0.13f)) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        using var candle = new SolidBrush(Candle);
        using var rind = new SolidBrush(Color.FromArgb(234, 118, 28));   // the pumpkin's own orange, for a bit left standing inside a hole
        using var nose = new SolidBrush(Candle);

        switch (face)
        {
            case 0:   // SURPRISED: wide round eyes, brows flung up high, a round "O" mouth
                g.FillEllipse(candle, Disc(-0.30f, -0.12f, 0.17f));
                g.FillEllipse(candle, Disc(0.30f, -0.12f, 0.17f));
                g.FillEllipse(rind, Disc(-0.30f, -0.12f, 0.06f));            // a small uncarved dot makes it a staring eye
                g.FillEllipse(rind, Disc(0.30f, -0.12f, 0.06f));
                g.DrawCurve(stroke, [P(-0.52f, -0.46f), P(-0.30f, -0.60f), P(-0.08f, -0.46f)]);   // raised brows, arched
                g.DrawCurve(stroke, [P(0.08f, -0.46f), P(0.30f, -0.60f), P(0.52f, -0.46f)]);
                g.FillEllipse(candle, new RectangleF(x - r * 0.20f, cy + r * 0.20f, r * 0.40f, r * 0.56f));   // the tall round mouth
                break;

            case 1:   // SAD: drooping eyes, brows sloping up in the middle, a tear, a frown
                g.FillEllipse(candle, new RectangleF(x - r * 0.46f, cy - r * 0.20f, r * 0.30f, r * 0.34f));
                g.FillEllipse(candle, new RectangleF(x + r * 0.16f, cy - r * 0.20f, r * 0.30f, r * 0.34f));
                g.DrawLine(stroke, P(-0.56f, -0.30f), P(-0.16f, -0.50f));     // inner ends high: worried
                g.DrawLine(stroke, P(0.56f, -0.30f), P(0.16f, -0.50f));
                g.FillPolygon(candle, [P(-0.36f, 0.20f), P(-0.43f, 0.34f), P(-0.36f, 0.42f), P(-0.29f, 0.34f)]);   // one tear
                g.DrawCurve(stroke, [P(-0.46f, 0.66f), P(-0.22f, 0.46f), P(0.0f, 0.42f), P(0.22f, 0.46f), P(0.46f, 0.66f)]);   // the frown: ends low
                break;

            case 2:   // WICKED: slanted slit eyes, a flat-topped nose, and a wide zigzag mouth
            {
                g.FillPolygon(candle, [P(-0.62f, -0.44f), P(-0.10f, -0.14f), P(-0.16f, 0.02f), P(-0.58f, -0.20f)]);
                g.FillPolygon(candle, [P(0.62f, -0.44f), P(0.10f, -0.14f), P(0.16f, 0.02f), P(0.58f, -0.20f)]);
                g.FillPolygon(candle, [P(-0.07f, 0.12f), P(0f, -0.04f), P(0.07f, 0.12f)]);
                // The mouth: a smile-shaped band whose top and bottom edges
                // are both zigzags, so it looks like two rows of teeth. The
                // outline is the top edge left to right, then the bottom
                // edge right to left. "Smile" lifts the ends.
                const int teeth = 7;
                var mouth = new List<PointF>();
                for (int k = 0; k <= teeth * 2; k++)
                {
                    float ax = -0.70f + 1.40f * k / (teeth * 2), lift = (ax * ax) * 0.30f;      // the smile curve: ends high, middle low
                    mouth.Add(P(ax, 0.20f - lift + (k % 2 == 1 ? 0.15f : 0f)));                  // odd points are the teeth tips, hanging down
                }
                for (int k = teeth * 2; k >= 0; k--)
                {
                    float ax = -0.70f + 1.40f * k / (teeth * 2), lift = (ax * ax) * 0.30f;
                    mouth.Add(P(ax, 0.62f - lift - (k % 2 == 1 ? 0.15f : 0f)));                  // bottom teeth point up
                }
                g.FillPolygon(candle, mouth.ToArray());
                break;
            }

            case 3:   // WINKING: one round open eye, one eye squeezed shut in an arc, a crooked smirk with a tooth
                g.FillEllipse(candle, Disc(-0.32f, -0.20f, 0.17f));
                g.FillEllipse(rind, Disc(-0.28f, -0.17f, 0.065f));
                g.DrawCurve(stroke, [P(0.14f, -0.12f), P(0.34f, -0.30f), P(0.54f, -0.12f)]);   // the shut eye: a happy arc
                g.FillPolygon(candle, [P(-0.07f, 0.08f), P(0f, -0.06f), P(0.07f, 0.08f)]);
                g.DrawCurve(stroke, [P(-0.46f, 0.36f), P(-0.16f, 0.52f), P(0.24f, 0.46f), P(0.60f, 0.20f)]);   // smirk: climbs on the right
                g.DrawLine(stroke, P(0.60f, 0.20f), P(0.66f, 0.08f));                                         // the cheek-dimple flick
                break;

            default:  // GOOFY: mismatched eyes (one big, one tiny), a wide grin with one buck tooth, tongue out
            {
                g.FillEllipse(candle, Disc(-0.32f, -0.12f, 0.23f));
                g.FillEllipse(rind, Disc(-0.25f, -0.17f, 0.09f));            // pupils looking off in different directions
                g.FillEllipse(candle, Disc(0.36f, -0.28f, 0.11f));
                g.FillEllipse(rind, Disc(0.38f, -0.25f, 0.05f));
                using var mouth = new GraphicsPath();
                mouth.AddLine(P(-0.58f, 0.22f), P(0.58f, 0.22f));
                mouth.AddBezier(P(0.58f, 0.22f), P(0.50f, 0.60f), P(-0.50f, 0.60f), P(-0.58f, 0.22f));
                g.FillPath(candle, mouth);
                g.FillRectangle(rind, new RectangleF(x - r * 0.20f, cy + r * 0.215f, r * 0.17f, r * 0.19f));    // the one buck tooth, left standing
                using var tongue = new SolidBrush(Color.FromArgb(226, 84, 76));
                g.FillEllipse(tongue, new RectangleF(x + r * 0.02f, cy + r * 0.40f, r * 0.30f, r * 0.40f));       // the tongue, hanging out over the lip
                using var crease = new Pen(Color.FromArgb(150, 40, 40), Math.Max(1f, r * 0.03f));
                g.DrawLine(crease, P(0.17f, 0.46f), P(0.17f, 0.68f));
                break;
            }
        }
    }

    /// <summary>A fade-in that starts at "from" and takes Fading seconds: 0 before, 1 after.</summary>
    private static float Ramp(float t, float from) => Smooth((t - from) / Fading);

    /// <summary>
    /// A candle flare: a quick swell to full and a slower fade, starting a
    /// little BEFORE the face begins to change so the eye is already pulled
    /// to the pumpkin when it happens.
    /// </summary>
    private static float Flare(float t, float change)
    {
        float k = (t - (change - 0.1f)) / 0.45f;          // 0 to 1 over 0.45 s
        return k <= 0 || k >= 1 ? 0 : MathF.Sin(MathF.PI * MathF.Pow(k, 0.6f));
    }

    public override void Draw(FrameBuffer fb, float t)
    {
        (PointF at, _) = _s.Pumpkins[_pumpkin];
        (int left, int top) = _where[_pumpkin];

        float toA = Ramp(t, ToA), toB = Ramp(t, ToB), back = 1 - Ramp(t, Back);

        // Which sprites are showing? While A is fading in, only A. Once B
        // starts fading in, A stays underneath (B is solid when it is done,
        // so A is hidden then). On the way back we fade only B: by then it
        // is the only one showing, so the face melts straight into the grin.
        // (No stencil: the pumpkins stand on the graveyard ground, in front of everything in the backdrop.)
        if (toB < 1f) _faces[_pumpkin][_a].Draw(fb, left, top, toA);
        if (toB > 0f) _faces[_pumpkin][_b].Draw(fb, left, top, Math.Min(toB, back));

        float flare = MathF.Max(Flare(t, ToA), MathF.Max(Flare(t, ToB), Flare(t, Back)));
        _flare[_pumpkin].DrawCentered(fb, at.X, at.Y, flare * 0.85f);
    }
}
