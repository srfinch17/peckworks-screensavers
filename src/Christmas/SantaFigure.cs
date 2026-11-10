using System.Drawing.Drawing2D;
using Peckworks.Screensavers.Core;

namespace Christmas;

/// <summary>
/// SANTA ON FOOT (for the story on the roof): the same Santa as in the sleigh, standing,
/// walking, hopping, rising out of the chimney sooty and dusting himself off. Painted facing
/// right with his FEET at one fixed spot on every page (FeetX, FeetY), so the story puts him
/// down by naming where his boots are, and mirrors the page to face left.
///
/// His size matches the Santa who sits in the sleigh (about 0.86 s from the floor to the
/// middle of his head), so the swap from one to the other does not jump.
///
/// What is in the flip-books:
///   - Stand / StandSack (the sack over his shoulder) / StandSoot
///   - Walk: eight pages, a plain walk: thigh and shin are two bones, the knee bends forward,
///     the arm opposite the forward leg swings forward; the sack rides on the shoulder (WalkSack)
///   - Rise: three pages of springing to his feet (a small anticipation)
///   - Hop: crouch, spring, tuck, fall, land (HopSack for the way out, HopNo for the way back)
///   - Slide: arms straight up holding the sack above his head (SlideSack), or just up (SlideNo,
///     the sooty PopSoot): only the part above the chimney's rim is drawn, so as he sinks the
///     arms and hat are the last of him
///   - Pat: four pages of patting soot off his coat, each cleaner than the last
/// </summary>
internal sealed class SantaFigure
{
    public sealed class Page { public required Sprite Img { get; init; } }

    private const float Scale = 1.2f;                      // the figure's drawing size against s (so he matches the seated Santa)
    public readonly int W, H, FeetX, FeetY;
    public readonly float S;

    public readonly Page Stand, StandSack, StandSoot, SlideNo, SlideSack, PopSoot;
    public readonly Page[] WalkSack, WalkNo, Rise, RiseNo, HopSack, HopNo, Pat;
    public readonly Page Front, FrontSack;                 // facing us: the middle of a turn on the spot (with and without the sack)

    private readonly record struct FPose(float Walk = -1f, float Squat = 0f, bool Sack = false, float ArmsUp = 0f, float Pat = -1f, float Tuck = 0f, float Soot = 0f);

    public SantaFigure(float s)
    {
        S = s;
        W = (int)MathF.Ceiling(1.9f * s / 2) * 2;
        H = (int)MathF.Ceiling(2.0f * s / 2) * 2;
        FeetX = W / 2; FeetY = (int)(1.76f * s);
        Stand = Make(new FPose());
        StandSack = Make(new FPose(Sack: true));
        StandSoot = Make(new FPose(Soot: 1f));
        SlideNo = Make(new FPose(ArmsUp: 1f));
        SlideSack = Make(new FPose(ArmsUp: 1f, Sack: true));
        PopSoot = Make(new FPose(ArmsUp: 1f, Soot: 1f));
        WalkSack = new Page[8]; WalkNo = new Page[8];
        for (int i = 0; i < 8; i++)
        {
            WalkSack[i] = Make(new FPose(Walk: i / 8f, Sack: true));
            WalkNo[i] = Make(new FPose(Walk: i / 8f));
        }
        Rise = new Page[3]; RiseNo = new Page[3];
        for (int i = 0; i < 3; i++)
        {
            Rise[i] = Make(new FPose(Squat: 0.5f * (1 - i / 2f), Sack: true));
            RiseNo[i] = Make(new FPose(Squat: 0.5f * (1 - i / 2f)));
        }
        Front = new Page { Img = Sprite.Paint(W, H, g => { g.TranslateTransform(FeetX, FeetY); g.ScaleTransform(S * Scale, S * Scale); DrawFront(g, false); }) };
        FrontSack = new Page { Img = Sprite.Paint(W, H, g => { g.TranslateTransform(FeetX, FeetY); g.ScaleTransform(S * Scale, S * Scale); DrawFront(g, true); }) };
        // Hop: 0 crouch, 1 spring (stretched), 2 tuck at the top, 3 falling, 4 landing crouch.
        float[] squat = [0.9f, 0f, 0f, 0.1f, 0.7f], tuck = [0f, 0f, 1f, 0.55f, 0f], up = [0f, 0.2f, 0.4f, 0.3f, 0f];
        HopSack = new Page[5]; HopNo = new Page[5];
        for (int i = 0; i < 5; i++)
        {
            HopSack[i] = Make(new FPose(Squat: squat[i], Tuck: tuck[i], Sack: true, ArmsUp: up[i] * 0f));
            HopNo[i] = Make(new FPose(Squat: squat[i], Tuck: tuck[i], ArmsUp: up[i]));
        }
        Pat = new Page[4];
        for (int i = 0; i < 4; i++) Pat[i] = Make(new FPose(Pat: i / 4f + 0.001f, Soot: 0.85f - 0.25f * i));
    }

    private Page Make(FPose p) => new() { Img = Sprite.Paint(W, H, g =>
    {
        g.TranslateTransform(FeetX, FeetY);
        g.ScaleTransform(S * Scale, S * Scale);
        Draw(g, p);
    }) };

    /// <summary>The knee for a two-bone leg (both bones 0.14): the knee sticks out FORWARD (+x), as a man's does.</summary>
    private static float Lerp(float a, float b, float k) => a + (b - a) * k;

    private static PointF Knee(PointF hip, ref PointF foot)
    {
        const float l = 0.14f;
        float dx = foot.X - hip.X, dy = foot.Y - hip.Y, d = MathF.Sqrt(dx * dx + dy * dy);
        if (d > 2 * l * 0.999f) { float k = 2 * l * 0.999f / d; dx *= k; dy *= k; d = 2 * l * 0.999f; foot = new PointF(hip.X + dx, hip.Y + dy); }
        if (d < 0.04f) d = 0.04f;
        float ux = dx / d, uy = dy / d, half = d / 2, h = MathF.Sqrt(MathF.Max(0, l * l - half * half));
        return new PointF(hip.X + ux * half + uy * h, hip.Y + uy * half - ux * h);
    }

    private void Draw(Graphics g, FPose p)
    {
        float sc = S * Scale;
        Pen St(Color c, float w) => SantaArt.Stroke(c, w, sc);
        PointF Pt(float x, float y) => new(x, y);
        Color red = Color.FromArgb(214, 38, 50), redDark = Color.FromArgb(152, 24, 38), white = Color.FromArgb(252, 252, 255), ink = Color.FromArgb(30, 22, 24);
        float squat = p.Squat;
        float hipY = -0.27f + 0.12f * squat;
        float torsoY = -0.48f + 0.13f * squat;
        float headY = torsoY - 0.25f;

        // ---- The sack over his back (behind him), unless it is held up over his head ----
        bool sackOver = p.Sack && p.ArmsUp > 0.5f;
        if (p.Sack && !sackOver)
        {
            using var bag = new SolidBrush(Color.FromArgb(140, 100, 62));
            using var bagLight = new SolidBrush(Color.FromArgb(172, 128, 84));
            g.FillClosedCurve(bag, [Pt(-0.12f, torsoY - 0.30f), Pt(-0.30f, torsoY - 0.22f), Pt(-0.36f, torsoY + 0.02f), Pt(-0.26f, torsoY + 0.16f), Pt(-0.08f, torsoY + 0.08f), Pt(-0.04f, torsoY - 0.14f)], FillMode.Winding, 0.5f);
            g.FillEllipse(bagLight, -0.30f, torsoY - 0.14f, 0.10f, 0.16f);
            using var box = new SolidBrush(Color.FromArgb(208, 36, 48));
            g.FillRectangle(box, -0.17f, torsoY - 0.36f, 0.08f, 0.07f);
        }

        // ---- Legs ----
        void Leg(float hx, float phase, bool near)
        {
            PointF hip = Pt(hx, hipY);
            PointF foot;
            if (p.Tuck > 0f) foot = Pt(hx + 0.10f * p.Tuck, hipY + 0.28f - 0.17f * p.Tuck);
            else if (p.Walk >= 0f)
            {
                float ph = (p.Walk + phase) % 1f * MathF.Tau;
                foot = Pt(hx + 0.17f * MathF.Sin(ph), -0.025f - 0.07f * MathF.Max(0f, MathF.Cos(ph)));
            }
            else foot = Pt(hx + (near ? 0.02f : -0.02f), -0.025f);
            PointF knee = Knee(hip, ref foot);
            using (var th = St(near ? red : redDark, 0.105f)) g.DrawLine(th, hip, knee);
            using (var sh = St(near ? red : redDark, 0.088f)) g.DrawLine(sh, knee, foot);
            using var boot = new SolidBrush(ink);
            g.FillEllipse(boot, foot.X - 0.07f + 0.025f, foot.Y - 0.035f, 0.14f, 0.065f);
        }
        Leg(-0.03f, 0.5f, false);

        // ---- The far arm (behind the coat) ----
        PointF shoulder = Pt(0.02f, torsoY - 0.13f);
        void Arm(Color col, float swing, bool near)
        {
            PointF elbow = Pt(shoulder.X + 0.05f + 0.06f * swing, shoulder.Y + 0.13f);
            PointF hand = Pt(shoulder.X + 0.07f + 0.17f * swing, shoulder.Y + 0.26f - 0.03f * MathF.Abs(swing));
            if (p.Walk < 0f) { swing = 0; elbow = Pt(shoulder.X + 0.05f, shoulder.Y + 0.13f); hand = Pt(shoulder.X + 0.08f, shoulder.Y + 0.27f); }
            if (p.Sack && !sackOver && near) { elbow = Pt(shoulder.X - 0.04f, shoulder.Y - 0.02f); hand = Pt(shoulder.X - 0.12f, shoulder.Y - 0.12f); }
            if (p.Pat >= 0f)
            {
                float pt = MathF.Sin(MathF.Tau * (p.Pat * 2f + (near ? 0f : 0.5f)));
                elbow = Pt(shoulder.X + 0.12f, shoulder.Y + 0.14f);
                hand = Pt(shoulder.X + 0.14f - 0.06f * pt, shoulder.Y + 0.24f + 0.03f * pt);
            }
            if (p.ArmsUp > 0f)
            {
                // Both arms go up beside the head (the near hand a little forward of it, the far one behind), so the sack overhead is gripped at its sides.
                float k = p.ArmsUp;
                float ex = near ? 0.17f : -0.12f, hxx = near ? 0.15f : -0.09f;
                elbow = Pt(Lerp(shoulder.X + 0.05f, ex, k), shoulder.Y + 0.13f - 0.36f * k);
                float reachUp = p.Sack ? 0.70f : 0.42f;               // gripping a sack overhead takes a long reach; waving or climbing out does not
                hand = Pt(Lerp(shoulder.X + 0.08f, hxx + (p.Sack ? 0f : (near ? 0.06f : -0.08f)), k), shoulder.Y + 0.27f - reachUp * k);
                if (!p.Sack) elbow = Pt(Lerp(shoulder.X + 0.05f, ex + (near ? 0.07f : -0.06f), k), shoulder.Y + 0.13f - 0.20f * k);
            }
            using (var arm = St(col, 0.09f)) g.DrawLines(arm, [shoulder, elbow, hand]);
            using (var mit = new SolidBrush(ink)) g.FillEllipse(mit, hand.X - 0.05f, hand.Y - 0.05f, 0.10f, 0.10f);
        }
        Arm(redDark, p.Walk >= 0f ? MathF.Sin((p.Walk + 0.5f) * MathF.Tau) * 0.6f : 0f, false);

        // ---- Coat, fur trim, belt ----
        using (var coat = new SolidBrush(red)) g.FillEllipse(coat, -0.20f, torsoY - 0.22f, 0.40f, 0.44f);
        using (var trim = St(white, 0.05f)) g.DrawLine(trim, Pt(-0.17f, torsoY + 0.17f), Pt(0.17f, torsoY + 0.17f));
        using (var belt = St(ink, 0.05f)) g.DrawLine(belt, Pt(-0.19f, torsoY + 0.05f), Pt(0.19f, torsoY + 0.05f));
        using (var buckle = new SolidBrush(Color.FromArgb(255, 214, 98))) g.FillRectangle(buckle, 0.0f, torsoY + 0.025f, 0.06f, 0.05f);

        Leg(0.03f, 0f, true);

        // ---- Head ----
        // A slightly big head, a clear white beard, a red hat with a fat pom-pom: he must still read at 40 pixels.
        float hx = 0.03f;
        using (var skin = new SolidBrush(Color.FromArgb(255, 214, 188))) g.FillEllipse(skin, hx - 0.125f, headY - 0.125f, 0.25f, 0.25f);
        using (var cheek = new SolidBrush(Color.FromArgb(130, 255, 130, 130))) g.FillEllipse(cheek, hx + 0.03f, headY + 0.0f, 0.085f, 0.055f);
        using (var wh = new SolidBrush(white))
        {
            g.FillClosedCurve(wh, [Pt(hx - 0.11f, headY - 0.005f), Pt(hx - 0.09f, headY + 0.16f), Pt(hx + 0.03f, headY + 0.25f), Pt(hx + 0.16f, headY + 0.15f), Pt(hx + 0.14f, headY + 0.02f)], FillMode.Winding, 0.5f);
            g.FillEllipse(wh, hx + 0.03f, headY + 0.05f, 0.16f, 0.07f);
        }
        using (var nose = new SolidBrush(Color.FromArgb(246, 150, 140))) g.FillEllipse(nose, hx + 0.12f, headY - 0.005f, 0.06f, 0.055f);
        using (var eye = new SolidBrush(ink)) g.FillEllipse(eye, hx + 0.05f, headY - 0.05f, 0.036f, 0.04f);
        using (var glint = new SolidBrush(Color.White)) g.FillEllipse(glint, hx + 0.062f, headY - 0.044f, 0.013f, 0.013f);
        using (var brow = St(white, 0.025f)) g.DrawLine(brow, Pt(hx + 0.035f, headY - 0.075f), Pt(hx + 0.10f, headY - 0.08f));
        using (var hat = new SolidBrush(red))
        {
            g.FillClosedCurve(hat, [Pt(hx - 0.125f, headY - 0.085f), Pt(hx - 0.02f, headY - 0.23f), Pt(hx - 0.20f, headY - 0.21f), Pt(hx - 0.38f, headY - 0.05f), Pt(hx - 0.22f, headY - 0.15f), Pt(hx + 0.08f, headY - 0.10f), Pt(hx + 0.125f, headY - 0.08f)], FillMode.Winding, 0.4f);
            using var pom = new SolidBrush(Color.White);
            g.FillEllipse(pom, hx - 0.46f, headY - 0.11f, 0.13f, 0.13f);
        }
        using (var brim = St(white, 0.075f)) g.DrawLine(brim, Pt(hx - 0.11f, headY - 0.09f), Pt(hx + 0.135f, headY - 0.085f));

        // ---- The sack held overhead ----
        if (sackOver)
        {
            using var bag = new SolidBrush(Color.FromArgb(140, 100, 62));
            g.FillClosedCurve(bag, [Pt(-0.15f, headY - 0.48f), Pt(-0.19f, headY - 0.34f), Pt(0.0f, headY - 0.27f), Pt(0.21f, headY - 0.34f), Pt(0.19f, headY - 0.48f), Pt(0.02f, headY - 0.54f)], FillMode.Winding, 0.5f);
            using var box = new SolidBrush(Color.FromArgb(208, 36, 48));
            g.FillRectangle(box, 0.02f, headY - 0.60f, 0.08f, 0.07f);
        }

        // ---- The near arm (in front of the coat) ----
        Arm(red, p.Walk >= 0f ? MathF.Sin(p.Walk * MathF.Tau) * 0.6f : 0f, true);

        // ---- Soot: smudges on the coat, the face and the beard's tips ----
        if (p.Soot > 0f)
        {
            int a = (int)(170 * MathF.Min(1f, p.Soot));
            using var soot = new SolidBrush(Color.FromArgb(a, 34, 30, 34));
            g.FillEllipse(soot, -0.14f, torsoY - 0.12f, 0.16f, 0.12f);
            g.FillEllipse(soot, 0.02f, torsoY + 0.02f, 0.14f, 0.10f);
            g.FillEllipse(soot, hx - 0.07f, headY - 0.07f, 0.09f, 0.07f);
            g.FillEllipse(soot, hx + 0.02f, headY + 0.10f, 0.12f, 0.07f);
            g.FillEllipse(soot, -0.10f, hipY + 0.02f, 0.13f, 0.12f);
        }
    }

    /// <summary>Santa facing us, standing: both legs, both arms, the face straight on. One page, used for half a turn.</summary>
    private void DrawFront(Graphics g, bool sack)
    {
        float sc = S * Scale;
        Pen St(Color c, float w) => SantaArt.Stroke(c, w, sc);
        PointF Pt(float x, float y) => new(x, y);
        Color red = Color.FromArgb(214, 38, 50), white = Color.FromArgb(252, 252, 255), ink = Color.FromArgb(30, 22, 24);
        using (var leg = St(red, 0.10f)) { g.DrawLine(leg, Pt(-0.07f, -0.27f), Pt(-0.08f, -0.05f)); g.DrawLine(leg, Pt(0.07f, -0.27f), Pt(0.08f, -0.05f)); }
        using (var boot = new SolidBrush(ink)) { g.FillEllipse(boot, -0.16f, -0.065f, 0.15f, 0.08f); g.FillEllipse(boot, 0.01f, -0.065f, 0.15f, 0.08f); }
        using (var arm = St(red, 0.09f)) { g.DrawLine(arm, Pt(-0.17f, -0.60f), Pt(-0.22f, -0.34f)); g.DrawLine(arm, Pt(0.17f, -0.60f), Pt(0.22f, -0.34f)); }
        using (var mit = new SolidBrush(ink)) { g.FillEllipse(mit, -0.27f, -0.39f, 0.10f, 0.10f); g.FillEllipse(mit, 0.17f, -0.39f, 0.10f, 0.10f); }
        using (var coat = new SolidBrush(red)) g.FillEllipse(coat, -0.21f, -0.70f, 0.42f, 0.46f);
        using (var trim = St(white, 0.05f)) g.DrawLine(trim, Pt(-0.17f, -0.31f), Pt(0.17f, -0.31f));
        using (var belt = St(ink, 0.05f)) g.DrawLine(belt, Pt(-0.20f, -0.43f), Pt(0.20f, -0.43f));
        using (var buckle = new SolidBrush(Color.FromArgb(255, 214, 98))) g.FillRectangle(buckle, -0.03f, -0.455f, 0.06f, 0.05f);
        float hy = -0.75f;
        using (var skin = new SolidBrush(Color.FromArgb(255, 214, 188))) g.FillEllipse(skin, -0.125f, hy - 0.125f, 0.25f, 0.25f);
        using (var cheek = new SolidBrush(Color.FromArgb(130, 255, 130, 130))) { g.FillEllipse(cheek, -0.11f, hy + 0.0f, 0.07f, 0.05f); g.FillEllipse(cheek, 0.04f, hy + 0.0f, 0.07f, 0.05f); }
        using (var wh = new SolidBrush(white))
        {
            g.FillClosedCurve(wh, [Pt(-0.12f, hy), Pt(-0.09f, hy + 0.16f), Pt(0.0f, hy + 0.25f), Pt(0.09f, hy + 0.16f), Pt(0.12f, hy)], FillMode.Winding, 0.5f);
            g.FillEllipse(wh, -0.10f, hy + 0.045f, 0.20f, 0.07f);
        }
        using (var nose = new SolidBrush(Color.FromArgb(246, 150, 140))) g.FillEllipse(nose, -0.03f, hy - 0.02f, 0.06f, 0.055f);
        using (var eye = new SolidBrush(ink)) { g.FillEllipse(eye, -0.075f, hy - 0.06f, 0.036f, 0.04f); g.FillEllipse(eye, 0.04f, hy - 0.06f, 0.036f, 0.04f); }
        using (var hat = new SolidBrush(red))
        {
            g.FillClosedCurve(hat, [Pt(-0.13f, hy - 0.08f), Pt(-0.06f, hy - 0.25f), Pt(0.10f, hy - 0.22f), Pt(0.22f, hy - 0.13f), Pt(0.25f, hy - 0.02f), Pt(0.13f, hy - 0.08f)], FillMode.Winding, 0.4f);
            using var pom = new SolidBrush(Color.White);
            g.FillEllipse(pom, 0.19f, hy - 0.09f, 0.13f, 0.13f);
        }
        using (var brim = St(white, 0.075f)) g.DrawLine(brim, Pt(-0.13f, hy - 0.085f), Pt(0.13f, hy - 0.085f));
        if (sack)                                                  // the sack, over one shoulder, beside him
        {
            using var bag = new SolidBrush(Color.FromArgb(140, 100, 62));
            g.FillClosedCurve(bag, [Pt(-0.20f, -0.80f), Pt(-0.36f, -0.66f), Pt(-0.38f, -0.40f), Pt(-0.26f, -0.30f), Pt(-0.14f, -0.40f), Pt(-0.12f, -0.66f)], FillMode.Winding, 0.5f);
        }
    }

    public void AddSheetRows(List<(string Name, List<(Sprite Img, int Cx, int Cy)> Pages)> rows)
    {
        var cx = W / 2 - 0; var cy = H / 2;
        // The sheet centres each page on the cell: the figure's pages share one size, so just use the page centre.
        List<(Sprite, int, int)> Row(params Page[] pages) => pages.Select(p => (p.Img, cx, cy)).ToList();
        void Add(string name, params Page[] pages) => rows.Add((name, Row(pages)));
        Add("santa: stand, sack, soot, slide, slide sack, pop soot", Stand, StandSack, StandSoot, SlideNo, SlideSack, PopSoot);
        Add("santa: walk with sack", WalkSack);
        Add("santa: walk", WalkNo);
        Add("santa: rise, rise (no sack), front, front sack, pat", [.. Rise, .. RiseNo, Front, FrontSack, .. Pat]);
        Add("santa: hop (sack), hop (no sack)", [.. HopSack, .. HopNo]);
    }
}
