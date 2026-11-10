using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using Peckworks.Screensavers.Core;

namespace Christmas;

/// <summary>The four kinds of bird that come to the table.</summary>
internal enum BirdKind { Cardinal, Hen, Chickadee, Jay }

/// <summary>
/// Every page of ONE kind of bird at ONE size: a flip-book painted once (on a helper thread) and then only stamped.
/// All pages face RIGHT (a bird going left is the same sticker turned over, Sprite.Draw's mirror). The origin of
/// every page is the bird's FEET: a perched page has its feet on (Ox, Oy); a flying page hangs the body a third
/// of a unit above that point with the feet tucked; a landing page is anchored so the front foot comes to rest
/// on the point the whole flight was aimed at. So the scene only ever says "the feet are HERE".
/// </summary>
internal sealed class BirdBook
{
    public const int FlapPages = 12;
    public int Ox, Oy;                                   // where the feet are in every page
    public float K;                                      // pixels in one bird unit (a bird is about 0.9 units, beak to tail)
    public readonly Sprite[] Head = new Sprite[5];       // the head turning: 0 forward, 2 facing us, 4 looking right back
    public readonly Sprite[] Sing = new Sprite[3];       // head up beak shut, beak half open, beak wide open
    public readonly Sprite[] Peck = new Sprite[2];       // the head going down: half way, and to the seed
    public readonly Sprite[] Flick = new Sprite[2];      // the tail up a little, the tail up a lot
    public readonly Sprite[] Hop = new Sprite[3];        // crouch, in the air with legs trailing, landing squat
    public readonly Sprite[] Hang = new Sprite[2];       // hanging upside down by the feet (looking ahead, looking at us)
    public readonly Sprite[] Bathe = new Sprite[2];      // wings thrown out, low: the two ends of a flutter
    public readonly Sprite[] Flap = new Sprite[FlapPages];
    public readonly Sprite[] Flare = new Sprite[3];      // the landing: wings thrown up, feet reaching, then the downstroke that settles it
    public Sprite Fluff = null!, Crouch = null!, Lean = null!, Alert = null!, Fold = null!, Shadow = null!;
    public int ShadowOx, ShadowOy;
}

/// <summary>
/// How the feeder birds are drawn. FEYNMAN VERSION: a bird is a handful of plump ovals (a body, a lighter breast, a
/// round head, a wing blob, a tail wedge) and thin legs, painted in "bird units" (x forward, y UP, the feet at the
/// origin) so the same drawing code serves four birds at any size: only the colours and a few proportions change.
///
/// PROPORTIONS (looked up before drawn, the way a field guide gives them; all as fractions of a bird 0.9 long):
///   Northern cardinal, 22 cm: plump, upright when perched; a big conical orange beak; a tall pointed crest; a black
///     face mask running from the beak to the chin; a long tail (0.4 of the bird). The male is all red, the hen buff
///     brown with red on the wings, tail and crest.
///   Black-capped chickadee, 13 cm: a ball with a head almost as big as the body; a black cap and bib with white
///     cheeks; grey back, pale belly, buff flanks; a tiny dark beak; a short tail.
///   Blue jay, 28 cm: bigger and longer in the tail; a blue crest and back, white face and belly, a black necklace,
///     white bars on the wing, a stout black beak.
///   A perched small bird's leg: a hidden thigh up in the feathers, the ankle bent BACKWARD just under the belly, a
///     thin bare shank, and three toes forward with one behind (the Thanksgiving birds' rule).
///
/// THE POSES: head turned by an angle (the face squeezes flat as it passes "facing us", the way a cartoon head
/// turns), head lifted for song, beak open, head dipped for a peck, tail flicked, crouched to spring, fluffed up into
/// a ball against the cold, leaning in with the beak open (a squabble), hanging upside down, wings thrown out to
/// bathe, and in the air the wing blade swinging round (12 pages), shut for the dip between bursts, and the landing
/// flare.
/// </summary>
internal static class FeederBirdsArt
{
    /// <summary>One perched pose, every number optional: the page is the bird with these dials turned.</summary>
    private readonly record struct Pose(float Psi = 0f, float Lift = 0f, float Open = 0f, float Dip = 0f, float Flick = 0f,
        float Crouch = 0f, float Fluff = 0f, float Lean = 0f, float Pitch = 0f, bool Hang = false, float Spread = -1f, float Air = 0f);

    /// <summary>The colours and proportions that make a cardinal a cardinal and not a jay.</summary>
    private sealed class Spec
    {
        public BirdKind Kind;
        public Color Back, Breast, WingC, WingEdge, TailC, HeadC, Legs, Beak, Mask;
        public float BodyCy, BodyRx, BodyRy, BodyTilt, BellyX, BellyDy, BellyRx, BellyRy;
        public float HeadX, HeadY, HeadR, TailLen, TailAng, WingLen, CrestLen;
    }

    private static Spec For(BirdKind kind) => kind switch
    {
        BirdKind.Cardinal => new Spec
        {
            Kind = kind, Back = Color.FromArgb(206, 34, 40), Breast = Color.FromArgb(232, 70, 58), WingC = Color.FromArgb(152, 24, 34),
            WingEdge = Color.FromArgb(118, 16, 26), TailC = Color.FromArgb(160, 30, 36), HeadC = Color.FromArgb(206, 34, 40),
            Legs = Color.FromArgb(150, 104, 94), Beak = Color.FromArgb(244, 138, 58), Mask = Color.FromArgb(30, 18, 20),
            BodyCy = 0.31f, BodyRx = 0.215f, BodyRy = 0.150f, BodyTilt = 28f, BellyX = 0.075f, BellyDy = -0.04f, BellyRx = 0.135f, BellyRy = 0.105f,
            HeadX = 0.175f, HeadY = 0.47f, HeadR = 0.10f, TailLen = 0.38f, TailAng = 215f, WingLen = 1f, CrestLen = 0.20f,
        },
        BirdKind.Hen => new Spec
        {
            Kind = kind, Back = Color.FromArgb(182, 146, 108), Breast = Color.FromArgb(214, 184, 142), WingC = Color.FromArgb(178, 100, 82),
            WingEdge = Color.FromArgb(128, 72, 62), TailC = Color.FromArgb(196, 84, 74), HeadC = Color.FromArgb(186, 150, 112),
            Legs = Color.FromArgb(150, 110, 96), Beak = Color.FromArgb(244, 150, 70), Mask = Color.FromArgb(104, 86, 78),
            BodyCy = 0.31f, BodyRx = 0.215f, BodyRy = 0.150f, BodyTilt = 28f, BellyX = 0.075f, BellyDy = -0.04f, BellyRx = 0.135f, BellyRy = 0.105f,
            HeadX = 0.175f, HeadY = 0.47f, HeadR = 0.10f, TailLen = 0.38f, TailAng = 215f, WingLen = 1f, CrestLen = 0.17f,
        },
        BirdKind.Chickadee => new Spec
        {
            Kind = kind, Back = Color.FromArgb(128, 138, 154), Breast = Color.FromArgb(236, 228, 214), WingC = Color.FromArgb(100, 110, 128),
            WingEdge = Color.FromArgb(214, 220, 230), TailC = Color.FromArgb(96, 106, 124), HeadC = Color.FromArgb(24, 24, 30),
            Legs = Color.FromArgb(64, 60, 66), Beak = Color.FromArgb(44, 44, 50), Mask = Color.FromArgb(24, 24, 30),
            BodyCy = 0.27f, BodyRx = 0.225f, BodyRy = 0.185f, BodyTilt = 16f, BellyX = 0.04f, BellyDy = -0.05f, BellyRx = 0.175f, BellyRy = 0.13f,
            HeadX = 0.17f, HeadY = 0.45f, HeadR = 0.125f, TailLen = 0.30f, TailAng = 208f, WingLen = 0.86f, CrestLen = 0f,
        },
        _ => new Spec
        {
            Kind = kind, Back = Color.FromArgb(84, 126, 208), Breast = Color.FromArgb(230, 234, 242), WingC = Color.FromArgb(56, 92, 170),
            WingEdge = Color.FromArgb(236, 240, 250), TailC = Color.FromArgb(70, 108, 190), HeadC = Color.FromArgb(88, 130, 212),
            Legs = Color.FromArgb(58, 54, 60), Beak = Color.FromArgb(30, 30, 36), Mask = Color.FromArgb(24, 26, 38),
            BodyCy = 0.31f, BodyRx = 0.225f, BodyRy = 0.155f, BodyTilt = 22f, BellyX = 0.06f, BellyDy = -0.05f, BellyRx = 0.17f, BellyRy = 0.12f,
            HeadX = 0.18f, HeadY = 0.47f, HeadR = 0.105f, TailLen = 0.44f, TailAng = 208f, WingLen = 1.22f, CrestLen = 0.25f,
        },
    };

    private static PointF P(float x, float y) => new(x, y);

    private static void Ell(Graphics g, float cx, float cy, float rx, float ry, Color c, float tilt = 0f)
    {
        var st = g.Save();
        g.TranslateTransform(cx, cy);
        g.RotateTransform(tilt);
        using var b = new SolidBrush(c);
        g.FillEllipse(b, -rx, -ry, rx * 2, ry * 2);
        g.Restore(st);
    }

    private static void Blob(Graphics g, PointF[] pts, Color c, float tension = 0.5f)
    {
        using var path = new GraphicsPath();
        path.AddClosedCurve(pts, tension);
        using var b = new SolidBrush(c);
        g.FillPath(b, path);
    }

    private static void Limb(Graphics g, PointF a, PointF b, float w, Color c)
    {
        using var pen = new Pen(c, w) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        g.DrawLine(pen, a, b);
    }

    /// <summary>The tail: a long wedge from its root along an angle (degrees, y up), a darker line down its middle.</summary>
    private static void TailWedge(Graphics g, Spec sp, PointF root, float angleDeg, float len)
    {
        float a = angleDeg * MathF.PI / 180f, ca = MathF.Cos(a), sa = MathF.Sin(a);
        PointF F(float along, float across) => P(root.X + along * ca - across * sa, root.Y + along * sa + across * ca);
        float wide = sp.Kind == BirdKind.Jay ? 1.4f : 1f;
        using (var b = new SolidBrush(sp.TailC))
            g.FillPolygon(b, [F(0, -0.035f * wide), F(len * 0.6f, -0.05f * wide), F(len, -0.042f * wide), F(len, 0.042f * wide), F(len * 0.6f, 0.05f * wide), F(0, 0.035f * wide)]);
        Limb(g, F(0.02f, 0), F(len * 0.95f, 0), 0.012f, sp.WingEdge);
        if (sp.Kind == BirdKind.Jay)                                                                  // the white tip and bars
        {
            using var tip = new SolidBrush(Color.FromArgb(236, 240, 250));
            g.FillPolygon(tip, [F(len * 0.86f, -0.046f * wide), F(len, -0.042f * wide), F(len, 0.042f * wide), F(len * 0.86f, 0.046f * wide)]);
        }
    }

    /// <summary>One wing blade swung about the shoulder: phase 0 to 2 pi is one beat (up and back, then down and back, the tip lagging).</summary>
    private static void Blade(Graphics g, Spec sp, PointF shoulder, float phase, Color c, float scale)
    {
        float sinP = MathF.Sin(phase), cosP = MathF.Cos(phase);
        float theta = (175f - 70f * sinP) * MathF.PI / 180f;
        float len = (0.36f + 0.18f * MathF.Abs(sinP)) * sp.WingLen * scale;
        float bend = -24f * cosP * MathF.PI / 180f;
        float ct = MathF.Cos(theta), st = MathF.Sin(theta);
        float broad = 0.8f + 0.2f * sp.WingLen;
        PointF Q(float along, float across)
        {
            float a = along, b = across * broad;
            if (a > 0.5f * len)                                                  // beyond half way the blade bends about its middle
            {
                float da = a - 0.5f * len, cb = MathF.Cos(bend), sb = MathF.Sin(bend);
                a = 0.5f * len + da * cb - b * sb;
                b = da * sb + b * cb;
            }
            return P(shoulder.X + a * ct - b * st, shoulder.Y + a * st + b * ct);
        }
        Blob(g, [Q(0, 0.055f), Q(0.45f * len, 0.07f), Q(len, 0.02f), Q(0.96f * len, -0.035f), Q(0.7f * len, -0.085f), Q(0.35f * len, -0.11f), Q(0, -0.095f)], c, 0.3f);
        Color edge = Color.FromArgb(150, sp.WingEdge);
        if (sp.Kind == BirdKind.Jay)
            for (int i = 0; i < 2; i++) Limb(g, Q(len * (0.45f + 0.22f * i), -0.09f), Q(len * (0.45f + 0.22f * i), 0.06f), 0.016f, edge);   // the white bars
        else
            for (int i = 0; i < 3; i++) Limb(g, Q(len * (0.55f + 0.12f * i), -0.03f), Q(len * (0.88f + 0.03f * i), -0.02f - 0.012f * i), 0.011f, edge);
    }

    /// <summary>The head's face: each kind's own markings, beak and eye. cs is cos of the turn (1 facing forward, 0 facing us, -1 looking back).</summary>
    private static void Face(Graphics g, Spec sp, PointF c, float cs, float open, float lift)
    {
        float ac = MathF.Abs(cs);
        float up = lift * 0.012f;
        float beakRoot, blen, beakHalf;
        if (ac < 0.2f && sp.Kind != BirdKind.Chickadee)                      // FACING US: both eyes, the beak a short wedge pointing at the viewer
        {
            if (sp.Kind == BirdKind.Jay) Ell(g, c.X, c.Y - 0.03f, 0.075f, 0.06f, Color.FromArgb(234, 238, 246));
            else { Ell(g, c.X, c.Y - 0.02f, 0.045f, 0.058f, sp.Mask); }
            using (var wedge = new SolidBrush(sp.Beak)) g.FillPolygon(wedge, [P(c.X - 0.035f, c.Y - 0.015f), P(c.X + 0.035f, c.Y - 0.015f), P(c.X, c.Y - 0.075f)]);
            foreach (float sx in (float[])[-1f, 1f])
            {
                Ell(g, c.X + sx * 0.05f, c.Y + 0.025f, 0.022f, 0.022f, Color.FromArgb(12, 8, 10));
                Ell(g, c.X + sx * 0.05f + 0.006f, c.Y + 0.033f, 0.008f, 0.008f, Color.FromArgb(252, 252, 246));
            }
            return;
        }
        switch (sp.Kind)
        {
            case BirdKind.Cardinal:
            case BirdKind.Hen:
            {
                float m = sp.Kind == BirdKind.Cardinal ? 1f : 0.72f;                                  // the hen's mask is smaller and softer
                Ell(g, c.X + 0.05f * cs, c.Y - 0.004f, (0.026f + 0.034f * ac) * m, 0.052f * m, sp.Mask);
                Ell(g, c.X + 0.032f * cs, c.Y - 0.058f, (0.024f + 0.026f * ac) * m, 0.046f * m, sp.Mask);
                beakRoot = 0.075f; blen = 0.085f; beakHalf = 0.034f;
                break;
            }
            case BirdKind.Chickadee:
                // The head was painted black (the cap and the nape); the white cheek goes over its lower part and the bib under it.
                Ell(g, c.X + 0.030f * cs, c.Y - 0.034f, 0.074f + 0.022f * ac, 0.074f, Color.FromArgb(244, 244, 240));
                Ell(g, c.X + 0.045f * cs, c.Y - 0.112f, 0.046f + 0.022f * ac, 0.044f, sp.Mask);
                beakRoot = 0.108f; blen = 0.058f; beakHalf = 0.022f;
                break;
            default:
                Ell(g, c.X + 0.045f * cs, c.Y - 0.030f, 0.060f + 0.024f * ac, 0.056f, Color.FromArgb(234, 238, 246));      // the white face
                Limb(g, P(c.X + 0.09f * cs, c.Y - 0.002f), P(c.X - 0.06f * cs, c.Y + 0.016f), 0.020f, sp.Mask);               // the dark line through the eye
                Ell(g, c.X + 0.0f * cs, c.Y - 0.100f, 0.104f, 0.032f, sp.Mask, -20f * cs);                                    // the necklace
                beakRoot = 0.090f; blen = 0.112f; beakHalf = 0.034f;
                break;
        }
        // The beak: two halves that part when it sings (a cardinal's is stout and orange, a chickadee's a dark pin, a jay's a black dagger).
        PointF b = P(c.X + beakRoot * cs, c.Y - 0.006f);
        float bl = blen * (cs >= 0f ? Math.Max(cs, 0.55f) : Math.Min(cs, -0.75f));
        Color low = Color.FromArgb(Math.Max(0, sp.Beak.R - 14), Math.Max(0, sp.Beak.G - 18), Math.Max(0, sp.Beak.B - 10));
        using (var upper = new SolidBrush(sp.Beak))
            g.FillPolygon(upper, [P(b.X, b.Y + beakHalf), P(b.X + bl, b.Y + 0.004f + up + open * 0.028f), P(b.X, b.Y - 0.004f)]);
        if (open > 0.05f)
            using (var inside = new SolidBrush(Color.FromArgb(120, 24, 30)))
                g.FillPolygon(inside, [P(b.X, b.Y - 0.004f), P(b.X + bl * 0.85f, b.Y + 0.004f + up + open * 0.026f), P(b.X + bl * 0.85f, b.Y - 0.002f + up - open * 0.034f)]);
        using (var lower = new SolidBrush(low))
            g.FillPolygon(lower, [P(b.X, b.Y - 0.004f), P(b.X + bl * 0.9f, b.Y - 0.002f + up - open * 0.040f), P(b.X, b.Y - beakHalf - 0.002f)]);
        // The eye: dark with a tiny bright glint, the storybook touch.
        float ex = sp.Kind == BirdKind.Chickadee ? 0.036f : 0.024f, ey = sp.Kind == BirdKind.Chickadee ? 0.012f : 0.022f;
        Ell(g, c.X + ex * cs, c.Y + ey, 0.021f, 0.021f, Color.FromArgb(12, 8, 10));
        Ell(g, c.X + (ex + 0.007f) * cs, c.Y + ey + 0.008f, 0.008f, 0.008f, Color.FromArgb(252, 252, 246));
    }

    /// <summary>A bird on its feet, every dial of Pose turned to taste.</summary>
    private static void Perched(Graphics g, Spec sp, Pose p)
    {
        if (p.Hang) g.ScaleTransform(1f, -1f);                      // hung by the feet: the whole bird upside down about its feet
        float cs = MathF.Cos(p.Psi);
        float drop = 0.05f * p.Crouch + 0.02f * p.Fluff;
        float cy = sp.BodyCy - drop;
        float fl = 1f + 0.17f * p.Fluff;                            // fluffed: a bigger, rounder body (negative: sleek and tall)

        // The legs, under the body: two bones from a hip hidden in the feathers, the ankle bent back, then the toes.
        float hipY = cy - 0.10f;
        using (var toe = new Pen(sp.Legs, 0.022f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            foreach (float hx in (float[])[-0.03f, 0.05f])
            {
                PointF hip = P(hx, hipY), foot = P(hx + 0.01f - 0.07f * p.Air, 0.01f + 0.11f * p.Air);
                float thigh = 0.5f * (hipY - 0.01f) + 0.01f, shank = thigh;
                float dx = foot.X - hip.X, dy = foot.Y - hip.Y, d = MathF.Min(MathF.Sqrt(dx * dx + dy * dy), thigh + shank - 0.002f);
                float cosA = Math.Clamp((thigh * thigh + d * d - shank * shank) / (2 * thigh * Math.Max(d, 0.001f)), -1f, 1f);
                float th = MathF.Atan2(dy, dx) - MathF.Acos(cosA);   // minus: the ankle swings BACK
                PointF hock = P(hip.X + thigh * MathF.Cos(th), hip.Y + thigh * MathF.Sin(th));
                Limb(g, hip, hock, 0.028f, sp.Legs);
                Limb(g, hock, foot, 0.022f, sp.Legs);
                g.DrawLine(toe, foot, P(foot.X + 0.075f, foot.Y - 0.001f));
                g.DrawLine(toe, foot, P(foot.X + 0.055f, foot.Y + 0.012f));
                g.DrawLine(toe, foot, P(foot.X - 0.040f, foot.Y));
            }

        // Everything above the legs leans about the hip (a forward lean for a peck or a squabble).
        var whole = g.Save();
        g.TranslateTransform(0.02f, hipY);
        g.RotateTransform(p.Lean);
        g.TranslateTransform(-0.02f, -hipY);

        if (p.Spread >= 0f) Blade(g, sp, P(0.02f, cy + 0.10f), p.Spread - 0.35f, sp.WingC, 0.9f);   // the far wing, out behind the body
        TailWedge(g, sp, P(-0.15f, cy - 0.06f), sp.TailAng - p.Flick - 18f * p.Crouch - 12f * p.Fluff, sp.TailLen);
        Ell(g, 0f, cy, sp.BodyRx * fl, sp.BodyRy * fl, sp.Back, sp.BodyTilt);
        Ell(g, sp.BellyX, cy + sp.BellyDy, sp.BellyRx * fl * (1f + 0.1f * p.Lift), sp.BellyRy * fl * (1f + 0.1f * p.Lift), sp.Breast, sp.BodyTilt);

        // The folded wing along the side, darker, with feather lines (a blade swung out instead when the bird is bathing).
        if (p.Spread < 0f)
        {
            float oy = cy - 0.31f;
            Blob(g, [P(0.10f, 0.38f + oy), P(0.0f, 0.43f + oy), P(-0.15f, 0.35f + oy), P(-0.27f, 0.21f + oy), P(-0.21f, 0.165f + oy), P(-0.05f, 0.24f + oy), P(0.08f, 0.31f + oy)], sp.WingC, 0.4f);
            Color line = Color.FromArgb(150, sp.WingEdge);
            if (sp.Kind == BirdKind.Jay)
                for (int i = 0; i < 2; i++) Limb(g, P(-0.01f - i * 0.085f, 0.40f + oy - i * 0.045f), P(-0.07f - i * 0.085f, 0.28f + oy - i * 0.045f), 0.018f, line);     // the white bars
            else
                for (int i = 0; i < 3; i++) Limb(g, P(-0.03f - i * 0.065f, 0.33f + oy - i * 0.035f), P(-0.10f - i * 0.065f, 0.25f + oy - i * 0.035f), 0.012f, line);
            if (sp.Kind == BirdKind.Chickadee) Limb(g, P(0.05f, 0.355f + oy), P(-0.20f, 0.20f + oy), 0.016f, Color.FromArgb(190, sp.WingEdge));                                // the pale wing edge
        }

        // The head: a neck joining it to the body, a round ball, a crest, the face.
        float headX = sp.HeadX - 0.035f * p.Lift + 0.07f * p.Dip - 0.03f * p.Fluff - 0.055f * (1f - cs);   // looking back swings the head back over the shoulder
        float headY = sp.HeadY - drop * 0.5f + 0.03f * p.Lift - 0.20f * p.Dip - 0.05f * p.Fluff;
        Ell(g, headX - 0.055f, headY - 0.05f, sp.HeadR, sp.HeadR * 0.95f, sp.Back, 40f);
        var head = g.Save();
        g.TranslateTransform(headX, headY);
        g.RotateTransform(p.Pitch);
        g.TranslateTransform(-headX, -headY);
        Ell(g, headX, headY, sp.HeadR, sp.HeadR * 0.94f, sp.HeadC);
        if (sp.CrestLen > 0f)
        {
            float cl = sp.CrestLen * (1f + 0.25f * p.Lift);
            Blob(g, [P(headX - 0.10f * cs - 0.012f * p.Lift * cs, headY + 0.005f + cl), P(headX + 0.045f * cs, headY + 0.080f), P(headX - 0.02f * cs, headY + 0.05f), P(headX - 0.09f * cs, headY + 0.045f)],
                sp.Kind == BirdKind.Hen ? Color.FromArgb(206, 96, 80) : sp.HeadC, 0.35f);
        }
        Face(g, sp, P(headX, headY), cs, p.Open, p.Lift);
        g.Restore(head);

        if (p.Spread >= 0f) Blade(g, sp, P(0.05f, cy + 0.10f), p.Spread, Color.FromArgb(Math.Min(255, sp.WingC.R + 26), Math.Min(255, sp.WingC.G + 24), Math.Min(255, sp.WingC.B + 24)), 1f);
        g.Restore(whole);
    }

    /// <summary>
    /// The bird in the air. phi is where the wings are in their beat; tilt the body's nose-up angle; feet how far the
    /// legs reach down for a landing (0 tucked, 1 out); lambda how far the page is anchored by the FOOT rather than
    /// by the body (0 = the body hangs a third of a unit above the origin, 1 = the front foot is on the origin).
    /// </summary>
    private static void Flyer(Graphics g, Spec sp, float phi, float tilt, bool fold, float feet, float lambda)
    {
        float tr = tilt * MathF.PI / 180f;
        float lx = 0.06f, ly = -0.10f - 0.22f * feet;                                   // the front foot, in the body's own frame
        float fx = lx * MathF.Cos(tr) - ly * MathF.Sin(tr), fy = lx * MathF.Sin(tr) + ly * MathF.Cos(tr);   // and after the tilt
        g.TranslateTransform(lambda * (-fx + 0.06f), (1f - lambda) * 0.33f + lambda * (-fy + 0.01f));   // the front foot lands where the STANDING bird's front foot is (0.06, 0.01): no jump to the stand page
        g.RotateTransform(tilt);

        float bodyRx = sp.BodyRx * 1.09f, bodyRy = sp.BodyRy * 0.84f, hr = sp.HeadR * 0.88f;
        if (!fold) Blade(g, sp, P(0.03f, 0.07f), phi - 0.35f, sp.WingC, 1f);                           // the far wing, behind the body
        TailWedge(g, sp, P(-0.19f, 0.0f), (fold ? 180f : 184f + 6f * MathF.Sin(phi)), sp.TailLen * 0.92f);

        if (feet > 0.05f)                                                                               // landing: the legs reach down and a little forward
            foreach (float hx in (float[])[-0.02f, 0.06f])
            {
                PointF knee = P(hx + 0.01f * feet, -0.10f - 0.10f * feet), foot = P(hx, -0.10f - 0.22f * feet);
                Limb(g, P(hx, -0.08f), knee, 0.026f, sp.Legs);
                Limb(g, knee, foot, 0.022f, sp.Legs);
                Limb(g, foot, P(foot.X + 0.07f, foot.Y - 0.005f), 0.020f, sp.Legs);
                Limb(g, foot, P(foot.X + 0.05f, foot.Y + 0.012f), 0.018f, sp.Legs);
            }
        else Limb(g, P(0f, -0.1f), P(0.08f, -0.11f), 0.03f, sp.Legs);                                  // feet tucked up under the belly

        Ell(g, 0f, 0f, bodyRx, bodyRy, sp.Back, 2f);
        Ell(g, 0.06f, -0.03f, sp.BellyRx * 0.95f, sp.BellyRy * 0.78f, sp.Breast, 2f);
        PointF head = P(0.235f, 0.05f);
        Ell(g, head.X - 0.05f, head.Y - 0.01f, hr * 0.9f, hr * 0.85f, sp.Back);
        Ell(g, head.X, head.Y, hr, hr * 0.95f, sp.HeadC);
        if (sp.CrestLen > 0f)                                                                           // the crest swept flat back in flight
            Blob(g, [P(head.X - 0.05f - sp.CrestLen * 0.85f, head.Y + 0.065f), P(head.X + 0.01f, head.Y + 0.075f), P(head.X - 0.03f, head.Y + 0.03f), P(head.X - 0.09f, head.Y + 0.035f)],
                sp.Kind == BirdKind.Hen ? Color.FromArgb(206, 96, 80) : sp.HeadC, 0.35f);
        Face(g, sp, head, 1f, 0f, 0f);

        if (fold)                                                                                       // the wings shut along the sides: the dart of a bounding flight
        {
            Ell(g, -0.06f, 0.02f, 0.30f, 0.085f * (0.8f + 0.2f * sp.WingLen), sp.WingC, -6f);
            if (sp.Kind == BirdKind.Jay) Limb(g, P(-0.02f, 0.06f), P(-0.04f, -0.03f), 0.016f, Color.FromArgb(150, sp.WingEdge));
        }
        else Blade(g, sp, P(0f, 0.07f), phi, Color.FromArgb(Math.Min(255, sp.WingC.R + 26), Math.Min(255, sp.WingC.G + 24), Math.Min(255, sp.WingC.B + 24)), 1f);
    }

    /// <summary>Paints every page of one kind at "k" pixels to the bird unit.</summary>
    public static BirdBook Build(BirdKind kind, float k)
    {
        Spec sp = For(kind);
        int w = (int)(2.3f * k) + 6, oy = (int)(1.2f * k) + 3, h = oy + (int)(0.8f * k) + 3, ox = w / 2;
        var bk = new BirdBook { Ox = ox, Oy = oy, K = k };
        Sprite Pg(Action<Graphics> paint) => Sprite.Paint(w, h, g =>
        {
            g.TranslateTransform(ox, oy);
            g.ScaleTransform(k, -k);
            paint(g);
        });
        Sprite Pose1(Pose p) => Pg(g => Perched(g, sp, p));

        float[] psi = [0f, 0.30f * MathF.PI, 0.5f * MathF.PI, 0.74f * MathF.PI, MathF.PI];
        for (int i = 0; i < 5; i++) { float a = psi[i]; bk.Head[i] = Pose1(new Pose(Psi: a)); }
        bk.Sing[0] = Pose1(new Pose(Lift: 1f, Lean: 8f, Pitch: 14f));
        bk.Sing[1] = Pose1(new Pose(Lift: 1f, Open: 0.5f, Lean: 8f, Pitch: 14f));
        bk.Sing[2] = Pose1(new Pose(Lift: 1f, Open: 1f, Lean: 8f, Pitch: 14f));
        bk.Peck[0] = Pose1(new Pose(Lean: -16f, Dip: 0.45f, Pitch: -22f));
        bk.Peck[1] = Pose1(new Pose(Lean: -34f, Dip: 1f, Pitch: -42f));
        bk.Flick[0] = Pose1(new Pose(Flick: 26f));
        bk.Flick[1] = Pose1(new Pose(Flick: 48f));
        bk.Hop[0] = Pose1(new Pose(Crouch: 0.9f, Lean: -4f));
        bk.Hop[1] = Pose1(new Pose(Crouch: -0.25f, Lean: 10f, Air: 1f, Flick: 10f));
        bk.Hop[2] = Pose1(new Pose(Crouch: 0.55f, Lean: -2f));
        bk.Hang[0] = Pose1(new Pose(Hang: true, Lean: -8f));
        bk.Hang[1] = Pose1(new Pose(Hang: true, Psi: 0.55f * MathF.PI, Lean: -8f));
        bk.Bathe[0] = Pose1(new Pose(Crouch: 1.1f, Fluff: 0.5f, Spread: 1.15f, Lean: -4f));
        bk.Bathe[1] = Pose1(new Pose(Crouch: 1.1f, Fluff: 0.5f, Spread: 4.25f, Lean: -4f));
        bk.Fluff = Pose1(new Pose(Fluff: 1f, Crouch: 0.5f));
        bk.Crouch = Pose1(new Pose(Crouch: 1f, Flick: 22f, Lean: -8f));
        bk.Lean = Pose1(new Pose(Lean: -15f, Dip: 0.28f, Open: 0.9f, Pitch: -4f, Fluff: 0.3f));
        bk.Alert = Pose1(new Pose(Lean: 9f, Lift: 0.55f, Fluff: -0.15f, Flick: 8f));
        for (int i = 0; i < BirdBook.FlapPages; i++) { float phi = MathF.Tau * i / BirdBook.FlapPages; bk.Flap[i] = Pg(g => Flyer(g, sp, phi, 0f, false, 0f, 0f)); }
        bk.Fold = Pg(g => Flyer(g, sp, 0f, -6f, true, 0f, 0f));
        (float tilt, float feet, float phi, float lam)[] fl = [(14f, 0.55f, 1.3f, 0.35f), (8f, 1f, 1.0f, 0.7f), (0f, 1f, 4.3f, 1f)];
        for (int i = 0; i < 3; i++) { var f = fl[i]; bk.Flare[i] = Pg(g => Flyer(g, sp, f.phi, f.tilt, false, f.feet, f.lam)); }

        // A soft blue shadow for a bird on the snow: a flat ellipse (night, moonlit: faint).
        int sw = Math.Max(6, (int)(0.95f * k)), sh = Math.Max(3, (int)(0.17f * k));
        bk.Shadow = Sprite.Paint(sw, sh, g => { using var b = new SolidBrush(Color.FromArgb(66, 52, 78, 150)); g.FillEllipse(b, 0, 0, sw - 1, sh - 1); });
        bk.ShadowOx = sw / 2; bk.ShadowOy = sh / 2;
        return bk;
    }

    /// <summary>
    /// The pose sheet: every page of every kind at the nearest size, labelled, on the snow's colour, and a strip of
    /// the walking and flying pages on night blue. Judge the birds here, at 4x, before judging them in the scene.
    /// </summary>
    public static void WriteSheet(BirdBook[] books, BirdKind[] kinds, string path)
    {
        int cell = (int)(books[0].K * 2.4f) + 4;
        string[] names = ["head0", "head1", "head2", "head3", "head4", "sing0", "sing1", "sing2", "peck0", "peck1", "flick0", "flick1", "hop0", "hop1", "hop2",
            "hang0", "hang1", "bathe0", "bathe1", "fluff", "crouch", "lean", "alert", "fold", "flare0", "flare1", "flare2"];
        int cols = names.Length + 12;
        int perRow = 14;
        int rowsPer = (cols + perRow - 1) / perRow;
        int labelH = 14, rowH = cell * 1 + labelH;
        int W = perRow * cell, H = books.Length * rowsPer * rowH + 12;
        var snow = new uint[W * H];
        Array.Fill(snow, 0xFFDEE9FCu);
        for (int bi = 0; bi < books.Length; bi++)
        {
            BirdBook b = books[bi];
            Sprite[] pages = [.. b.Head, .. b.Sing, .. b.Peck, .. b.Flick, .. b.Hop, .. b.Hang, .. b.Bathe, b.Fluff, b.Crouch, b.Lean, b.Alert, b.Fold, .. b.Flare, .. b.Flap];
            for (int i = 0; i < pages.Length; i++)
            {
                int cx = (i % perRow) * cell, cy = 12 + (bi * rowsPer + i / perRow) * rowH + labelH;
                int ox = cx + cell / 2 - pages[i].Width / 2, oy = cy + (cell - pages[i].Height) / 2;
                // night-blue ground for the flight pages (the last twelve), snow for the rest
                if (i >= pages.Length - BirdBook.FlapPages)
                    for (int yy = 0; yy < cell; yy++) for (int xx = 0; xx < cell; xx++) snow[(cy + yy) * W + cx + xx] = 0xFF14245Cu;
                Blit(snow, W, H, pages[i], ox, oy);
            }
        }
        using var bmp = new Bitmap(W, H, PixelFormat.Format32bppArgb);
        var data = bmp.LockBits(new Rectangle(0, 0, W, H), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        System.Runtime.InteropServices.Marshal.Copy((int[])(object)snow, 0, data.Scan0, snow.Length);
        bmp.UnlockBits(data);
        using (var g = Graphics.FromImage(bmp))
        using (var f = new Font("Segoe UI", 8f))
        {
            for (int bi = 0; bi < books.Length; bi++)
                g.DrawString(kinds[bi].ToString() + "  (k " + books[bi].K.ToString("0.0") + ")  head0-4 sing0-2 peck0-1 flick0-1 hop0-2 hang0-1 bathe0-1 fluff crouch lean alert fold flare0-2, then flap 0-11 on blue", f, Brushes.Black, 2, 12 + bi * rowsPer * rowH);
        }
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        bmp.Save(path, ImageFormat.Png);
    }

    private static void Blit(uint[] canvas, int W, int H, Sprite s, int left, int top)
    {
        ReadOnlySpan<uint> src = s.Argb;
        for (int y = 0; y < s.Height; y++)
            for (int x = 0; x < s.Width; x++)
            {
                uint c = src[y * s.Width + x];
                int a = (int)(c >> 24);
                int dx = left + x, dy = top + y;
                if (a == 0 || dx < 0 || dy < 0 || dx >= W || dy >= H) continue;
                uint bg = canvas[dy * W + dx];
                int r = (int)((bg >> 16) & 255) + ((((int)((c >> 16) & 255)) - (int)((bg >> 16) & 255)) * a >> 8);
                int gg = (int)((bg >> 8) & 255) + ((((int)((c >> 8) & 255)) - (int)((bg >> 8) & 255)) * a >> 8);
                int bb = (int)(bg & 255) + ((((int)(c & 255)) - (int)(bg & 255)) * a >> 8);
                canvas[dy * W + dx] = 0xFF000000u | (uint)(r << 16) | (uint)(gg << 8) | (uint)bb;
            }
    }
}
