using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using Peckworks.Screensavers.Core;

namespace Christmas;

// ============================================================================
// THE ART OF THE HARES AND THE FOX (part one: the shared tools and the hare;
// part two, the fox, is FoxAndHaresFoxArt.cs, the same static class).
//
// FEYNMAN VERSION. An animal here is a flip-book: a pile of little pictures,
// each painted ONCE at the start, and the saver stamps the right page each
// frame. A page is painted from a POSE: a plain list of numbers (where the
// body is, where each foot is, how high the ears stand) handed to a painter
// that turns the numbers into shapes. Because a pose is only numbers, two
// poses can be blended (halfway between "sitting" and "standing tall" is a
// pose too), and that is how every "get up" and "settle down" is made: a short
// run of pages that are the blend, so nothing ever jumps from one picture to a
// different one.
//
// UNITS. Everything below is drawn in the animal's own unit (a hare: one
// BODY LENGTH, about 0.45 m; a fox: ONE METRE), x forward (the animal faces
// right; the saver mirrors the page to face left), y UP, the origin on the snow
// under the middle of the body. The caller scales a unit to pixels by the
// field's one scale (ChristmasScenery.Metre), so a fox by the pines and a fox by
// the log are the same fox.
//
// A LEG is JOINTED: two (or three) bones and a knee. We say where the hip is and
// where the foot is, and a small sum ("inverse kinematics") finds where the knee
// has to be so the bones reach: the way you bend your arm to touch a spot on a
// table, you choose the spot and the elbow goes where it must. A hind leg's hock
// (the "backward knee") bends BACKWARD, which is how every hare, fox and deer
// walks on its toes.
//
// COLOURS: it is night and the snow is (222, 233, 252). A white hare on it needs
// a warmer white, a grey-blue shaded back and dark ear tips; a fox is rust with
// a cream chest and black stockings. NO OUTLINES anywhere: shapes are told apart
// by tone, never by a pen round them.
// ============================================================================

/// <summary>One flip-book's recipe: how many pages, how much room each needs (in units), and how to paint page i.</summary>
internal sealed class BookSpec
{
    public string Name = "";
    public int N;
    public float HalfW, Above;                 // the sheet is 2*HalfW wide and Above (over the snow) plus a little under it tall
    public Action<Graphics, int> Paint = null!; // paints page i in unit space (origin at the feet, y up, facing +x)
    public Func<int, PointF>? Aux;              // a point of interest on page i (the nose, for the breath), in units
    public Func<int, PointF[]>? Keys;           // a few body points of page i (head, body, feet...), in units: only the mind log reads them, to measure a pose pop
}

/// <summary>One flip-book painted at every size step: Pages[tier][page]. The tier is the animal's distance from the viewer.</summary>
internal sealed class Book
{
    public readonly Sprite[][] Pages;
    public readonly PointF[] Aux;               // per page, in units
    public readonly PointF[][] Keys;            // per page, a few body points in units (empty when the book gives none)
    public readonly int[] Ox, Oy;               // per tier: the page pixel that lands on the animal's feet
    public readonly int Count;
    public readonly string Name;

    public Book(BookSpec spec, float[] tierPx)
    {
        Name = spec.Name; Count = spec.N;
        Pages = new Sprite[tierPx.Length][];
        Ox = new int[tierPx.Length]; Oy = new int[tierPx.Length];
        Aux = new PointF[spec.N];
        Keys = new PointF[spec.N][];
        for (int i = 0; i < spec.N; i++) { if (spec.Aux != null) Aux[i] = spec.Aux(i); Keys[i] = spec.Keys != null ? spec.Keys(i) : Array.Empty<PointF>(); }
        for (int t = 0; t < tierPx.Length; t++)
        {
            float k = tierPx[t];
            int w = (int)MathF.Ceiling(2 * spec.HalfW * k) + 4, below = Math.Max(2, (int)(0.10f * k)), h = (int)MathF.Ceiling(spec.Above * k) + below + 3;
            Ox[t] = w / 2; Oy[t] = h - below - 1;
            Pages[t] = new Sprite[spec.N];
            for (int i = 0; i < spec.N; i++)
            {
                int pi = i, ox = Ox[t], oy = Oy[t];
                Pages[t][i] = Sprite.Paint(w, h, g =>
                {
                    g.TranslateTransform(ox, oy);
                    g.ScaleTransform(k, -k);            // from here on: units, y up
                    spec.Paint(g, pi);
                });
            }
        }
    }
}

internal static partial class FoxAndHaresArt
{
    // ---------------------------------------------------------------- tools

    public static PointF P(float x, float y) => new(x, y);
    public static float Lerp(float a, float b, float t) => a + (b - a) * t;
    public static PointF Lerp(PointF a, PointF b, float t) => new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
    public static float Sm(float x) { x = Math.Clamp(x, 0f, 1f); return x * x * (3f - 2f * x); }
    public static PointF Add(PointF a, PointF b) => new(a.X + b.X, a.Y + b.Y);
    public static PointF Rot(PointF p, float deg)
    {
        float a = deg * MathF.PI / 180f, c = MathF.Cos(a), s = MathF.Sin(a);
        return new PointF(p.X * c - p.Y * s, p.X * s + p.Y * c);
    }
    static float Dist(PointF a, PointF b) => MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
    static Color Mix(Color a, Color b, float t) => Color.FromArgb((int)Lerp(a.A, b.A, t), (int)Lerp(a.R, b.R, t), (int)Lerp(a.G, b.G, t), (int)Lerp(a.B, b.B, t));

    /// <summary>A filled ellipse (cx, cy its middle, rx and ry its half sizes), tipped "tilt" degrees counter-clockwise.</summary>
    static void Ell(Graphics g, float cx, float cy, float rx, float ry, Color c, float tilt = 0f)
    {
        if (rx < 0.0005f || ry < 0.0005f) return;
        var st = g.Save();
        g.TranslateTransform(cx, cy);
        g.RotateTransform(tilt);
        using var b = new SolidBrush(c);
        g.FillEllipse(b, -rx, -ry, rx * 2, ry * 2);
        g.Restore(st);
    }
    static void Ell(Graphics g, PointF p, float rx, float ry, Color c, float tilt = 0f) => Ell(g, p.X, p.Y, rx, ry, c, tilt);

    /// <summary>A leaf shape lying from point a to point b with width w: how an ear, a thigh or a long hare foot is drawn.</summary>
    static void EllSeg(Graphics g, PointF a, PointF b, float w, Color c, float extra = 0.0f)
    {
        float len = Dist(a, b);
        float ang = MathF.Atan2(b.Y - a.Y, b.X - a.X) * 180f / MathF.PI;
        Ell(g, (a.X + b.X) / 2, (a.Y + b.Y) / 2, len / 2 + extra, w / 2, c, ang);
    }

    /// <summary>A thick bar with round ends: a leg bone.</summary>
    static void Limb(Graphics g, PointF a, PointF b, float w, Color c)
    {
        using var pen = new Pen(c, w) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        g.DrawLine(pen, a, b);
    }

    /// <summary>A smooth closed shape through the points.</summary>
    static void Blob(Graphics g, Color c, params PointF[] pts)
    {
        using var path = new GraphicsPath();
        path.AddClosedCurve(pts, 0.5f);
        using var b = new SolidBrush(c);
        g.FillPath(b, path);
    }
    static void Poly(Graphics g, Color c, params PointF[] pts)
    {
        using var b = new SolidBrush(c);
        g.FillPolygon(b, pts);
    }

    /// <summary>
    /// Two-bone leg: where must the knee (or elbow) be so a bone of l1 from the hip and a bone of l2 to the foot
    /// meet? Two circles cross at two places; "forward" picks the one with the larger x (a knee in front), else the
    /// one behind (an elbow, or a hock).
    /// </summary>
    static PointF Knee(PointF hip, PointF foot, float l1, float l2, bool forward)
    {
        float dx = foot.X - hip.X, dy = foot.Y - hip.Y;
        float real = MathF.Sqrt(dx * dx + dy * dy);
        if (real < 1e-5f) return hip;
        float d = Math.Clamp(real, MathF.Abs(l1 - l2) + 0.004f, l1 + l2 - 0.002f);
        float a = (l1 * l1 - l2 * l2 + d * d) / (2 * d);
        float h = MathF.Sqrt(MathF.Max(0f, l1 * l1 - a * a));
        float ux = dx / real, uy = dy / real;
        PointF k1 = P(hip.X + ux * a - uy * h, hip.Y + uy * a + ux * h), k2 = P(hip.X + ux * a + uy * h, hip.Y + uy * a - ux * h);
        return forward == (k1.X >= k2.X) ? k1 : k2;
    }

    /// <summary>The soft blue shadow on the moonlit snow under an animal: a flat ellipse, up to about 70 alpha.</summary>
    static void Shadow(Graphics g, float cx, float rx, float ry, float k = 1f)
    {
        if (k <= 0.02f) return;
        Ell(g, cx, 0f, rx * (0.6f + 0.4f * k), ry, Color.FromArgb((int)(64 * k), 50, 70, 150));
    }

    // ================================================================ THE HARE
    //
    // A SNOWSHOE HARE, looked up before drawn (unit = one body length, 0.45 m):
    //   - sitting height of the back about 0.5 body length, the head a ball of radius 0.15 on the chest,
    //   - the EARS long, 0.23 (about a third of the head to tail), black tipped, held up and a touch back,
    //   - the HIND FOOT is enormous, 0.30 long, a snowshoe, and lies FLAT on the snow when sitting; the hock
    //     (the heel) bends BACKWARD and the thigh is a big round haunch,
    //   - the FORELEGS are short and thin (two bones of 0.13), elbows back,
    //   - the TAIL a small round puff.
    // Its gait is the LOLLOP: forefeet land first, then the hind feet swing through OUTSIDE and AHEAD of them,
    // and the long hind feet push off last. Coat in winter: white, a grey-blue shaded back, black ear tips.
    // ----------------------------------------------------------------

    public static readonly Color HareCoat = Color.FromArgb(247, 245, 241);
    public static readonly Color HareBelly = Color.FromArgb(253, 252, 250);
    public static readonly Color HareShade = Color.FromArgb(168, 184, 212);     // the grey-blue back
    public static readonly Color HareFar = Color.FromArgb(196, 206, 226);       // the far side's limbs and ear, a shade down
    static readonly Color HareEarIn = Color.FromArgb(232, 190, 198);
    static readonly Color HareTip = Color.FromArgb(40, 34, 44);
    static readonly Color HareNose = Color.FromArgb(226, 150, 160);
    static readonly Color Ink = Color.FromArgb(28, 24, 34);

    /// <summary>One hare pose: where the body, head, ears and each foot are (units: one body length).</summary>
    public sealed class HPose
    {
        public PointF Body = P(0f, 0.27f);
        public float Rx = 0.36f, Ry = 0.25f, Tilt = 30f;     // the body ellipse; Tilt degrees nose-up
        public PointF Head = P(0.37f, 0.53f);
        public float HeadTilt;                                // degrees, plus = nose up
        public float EarL = 98f, EarR = 90f;                  // far and near ear: degrees from straight forward, counter-clockwise (90 straight up, 150 laid back)
        public float BendL, BendR;                            // 0 straight, 1 the tip folded down over the face
        public PointF ForeF = P(0.33f, 0f), ForeN = P(0.40f, 0f);
        public PointF ToeF = P(0.12f, 0f), ToeN = P(0.18f, 0f);
        public float AngF, AngN;                              // how far the hind foot is tipped up on its toes, degrees
        public float Nose;                                    // the nose's twitch, units forward
        public float Yaw;                                     // 0 side on, 90 facing us
        public float Sink;                                    // how far down into the snow the whole animal is
        public float Blink;                                   // 0 open .. 1 shut
        public float Chew;                                    // 0 .. 1 the jaw's bob
        public bool Clip;                                     // hide everything below the snow line (a burrow)
        public float Shadow = 1f;
        public float Lip;                                     // 0 .. 1 the snow lip drawn over a burrow's edge
        public float LipX = 0.05f;                            // where the lip's middle is (the hole's place on the page)
    }

    public static HPose HLerp(HPose a, HPose b, float t) => new()
    {
        Body = Lerp(a.Body, b.Body, t), Rx = Lerp(a.Rx, b.Rx, t), Ry = Lerp(a.Ry, b.Ry, t), Tilt = Lerp(a.Tilt, b.Tilt, t),
        Head = Lerp(a.Head, b.Head, t), HeadTilt = Lerp(a.HeadTilt, b.HeadTilt, t),
        EarL = Lerp(a.EarL, b.EarL, t), EarR = Lerp(a.EarR, b.EarR, t), BendL = Lerp(a.BendL, b.BendL, t), BendR = Lerp(a.BendR, b.BendR, t),
        ForeF = Lerp(a.ForeF, b.ForeF, t), ForeN = Lerp(a.ForeN, b.ForeN, t), ToeF = Lerp(a.ToeF, b.ToeF, t), ToeN = Lerp(a.ToeN, b.ToeN, t),
        AngF = Lerp(a.AngF, b.AngF, t), AngN = Lerp(a.AngN, b.AngN, t), Nose = Lerp(a.Nose, b.Nose, t), Yaw = Lerp(a.Yaw, b.Yaw, t),
        Sink = Lerp(a.Sink, b.Sink, t), Blink = Lerp(a.Blink, b.Blink, t), Chew = Lerp(a.Chew, b.Chew, t),
        Clip = a.Clip || b.Clip, Shadow = Lerp(a.Shadow, b.Shadow, t), Lip = Lerp(a.Lip, b.Lip, t), LipX = Lerp(a.LipX, b.LipX, t),
    };

    /// <summary>The geometry of one ear in side view: where it starts on the head, its bend, its tip. Shared by the painter and by a pose that holds the tip in a paw.</summary>
    public static void EarGeom(HPose q, bool near, out PointF root, out PointF mid, out PointF tip)
    {
        float ang = near ? q.EarR : q.EarL, bend = near ? q.BendR : q.BendL;
        root = Add(q.Head, Rot(P(-0.045f, 0.125f), q.HeadTilt));
        float a = ang * MathF.PI / 180f;
        mid = P(root.X + 0.150f * MathF.Cos(a), root.Y + 0.150f * MathF.Sin(a));
        float a2 = (ang - bend * 150f) * MathF.PI / 180f;                  // the tip folds over, forward and down
        tip = P(mid.X + 0.130f * MathF.Cos(a2), mid.Y + 0.130f * MathF.Sin(a2));
    }

    public static void PaintHare(Graphics g, HPose q)
    {
        float yaw = q.Yaw * MathF.PI / 180f, c = MathF.Cos(yaw), s = MathF.Sin(yaw);
        PointF Pr(PointF p, float lat = 0f) => P(p.X * c + lat * s, p.Y);       // side view point -> as seen at this yaw (lat: how far toward the viewer)
        Shadow(g, 0.10f, 0.50f, 0.07f, q.Shadow);
        if (q.Clip) g.SetClip(new RectangleF(-4f, 0f, 8f, 6f));                 // below the snow line nothing shows
        g.TranslateTransform(0f, -q.Sink);

        float tilt = q.Tilt * c, hr = 0.15f;
        float brx = MathF.Sqrt(q.Rx * c * q.Rx * c + 0.17f * s * 0.17f * s);

        void Ear(bool near, Color col)
        {
            EarGeom(q, near, out PointF root, out PointF mid, out PointF tip);
            float lat = near ? 0.05f : -0.05f, splay = near ? 0.13f : -0.13f;
            PointF r = Pr(root, lat), m = Pr(mid, lat * 1.8f), t = Pr(tip, splay);
            EllSeg(g, r, m, 0.095f, col, 0.01f);
            EllSeg(g, m, t, 0.075f, Mix(col, HareShade, 0.40f), 0.0f);           // the upper ear a shade down: white on pale snow would vanish and leave the black tip floating
            if (near) EllSeg(g, Lerp(r, m, 0.15f), Lerp(r, m, 0.85f), 0.040f, HareEarIn);
            Ell(g, Lerp(m, t, 0.80f), 0.036f, 0.028f, HareTip, MathF.Atan2(t.Y - m.Y, t.X - m.X) * 57.2958f);   // the black tip
        }

        void Hind(PointF toe, float ang, float lat, Color tone)
        {
            PointF hip = Add(q.Body, Rot(P(-q.Rx * 0.42f, -q.Ry * 0.05f), q.Tilt));
            float a = ang * MathF.PI / 180f;
            PointF heel = P(toe.X - 0.30f * MathF.Cos(a), toe.Y + 0.30f * MathF.Sin(a));
            PointF hp = Pr(hip, lat), tp = Pr(toe, lat), hl = Pr(heel, lat);
            PointF kn = Knee(hp, hl, 0.21f, 0.22f, true);
            EllSeg(g, hp, kn, 0.20f, tone, 0.02f);                  // the haunch: a big round thigh
            Limb(g, kn, hl, 0.075f, tone);                           // the shank down to the heel
            EllSeg(g, hl, tp, 0.082f, tone, 0.012f);                 // the long snowshoe foot
        }

        void Fore(PointF paw, float lat, Color tone)
        {
            PointF sh = Add(q.Body, Rot(P(q.Rx * 0.58f, -q.Ry * 0.35f), q.Tilt));
            PointF sp = Pr(sh, lat), pp = Pr(paw, lat);
            PointF el = Knee(sp, pp, 0.14f, 0.14f, false);
            Limb(g, sp, el, 0.072f, tone);
            Limb(g, el, pp, 0.056f, tone);
            Ell(g, pp.X + 0.012f, pp.Y + 0.008f, 0.04f, 0.026f, tone);
        }

        // far ear, far hind leg and far foreleg first (a shade darker), then the body, then everything on the near side.
        Ear(false, HareFar);
        Hind(q.ToeF, q.AngF, -0.07f, HareFar);
        Fore(q.ForeF, -0.07f, HareFar);

        PointF bc = Pr(q.Body);
        Ell(g, bc, brx, q.Ry, HareCoat, tilt);
        Ell(g, Pr(Add(q.Body, Rot(P(-0.05f, q.Ry * 0.36f), q.Tilt))), brx * 0.78f, q.Ry * 0.44f, HareShade, tilt);    // the grey-blue back
        Ell(g, Pr(Add(q.Body, Rot(P(0.06f, -q.Ry * 0.5f), q.Tilt))), brx * 0.72f, q.Ry * 0.40f, HareBelly, tilt);       // the pale belly
        PointF tailAt = Pr(Add(q.Body, Rot(P(-q.Rx * 0.98f, q.Ry * 0.12f), q.Tilt)), -0.02f);
        Ell(g, tailAt, 0.085f, 0.08f, HareBelly);                                                                       // the puff of a tail
        Ell(g, tailAt.X, tailAt.Y + 0.02f, 0.07f, 0.04f, Color.FromArgb(150, HareShade));

        Hind(q.ToeN, q.AngN, 0.07f, HareCoat);

        // the head
        PointF H = q.Head;
        PointF HL(float f, float y) => Add(H, Rot(P(f, y), q.HeadTilt));
        Ear(true, HareCoat);
        float hw = MathF.Sqrt(1.1f * hr * c * 1.1f * hr * c + hr * s * hr * s);
        Ell(g, Pr(H), hw, hr * 0.97f, HareCoat, q.HeadTilt * c);
        Ell(g, Pr(HL(hr * 0.80f, -hr * 0.30f)), hr * 0.62f * (0.35f + 0.65f * c), hr * 0.50f * (1f + 0.22f * q.Chew), HareBelly, q.HeadTilt * c);
        Ell(g, Pr(HL(hr * 0.2f, hr * 0.5f)), hw * 0.8f, hr * 0.38f, Color.FromArgb(120, HareShade), q.HeadTilt * c);    // the grey brow
        PointF nose = Pr(HL(hr * 1.30f + q.Nose, -hr * 0.12f));
        Ell(g, nose, 0.030f, 0.024f, HareNose);
        void Eye(float lat)
        {
            PointF e = Pr(HL(hr * 0.42f, hr * 0.26f), lat);
            float ry = 0.046f * (1f - q.Blink * 0.88f);
            Ell(g, e, 0.040f, ry, Ink);
            if (q.Blink < 0.5f) Ell(g, e.X + 0.012f, e.Y + 0.015f, 0.014f, 0.014f, Color.White);   // the round dark eye's one white glint
        }
        if (q.Yaw > 8f) Eye(-0.075f);
        Eye(0.075f);

        Fore(q.ForeN, 0.07f, HareCoat);

        g.TranslateTransform(0f, q.Sink);
        if (q.Clip) g.ResetClip();
        if (q.Lip > 0.01f)
        {
            // the lip of snow thrown up round a burrow: a pale low mound with a bluer underside, drawn over the cut edge.
            Ell(g, q.LipX, -0.01f, 0.34f * q.Lip + 0.04f, 0.052f * q.Lip + 0.004f, Color.FromArgb(205, 218, 244));
            Ell(g, q.LipX, 0.012f, 0.32f * q.Lip + 0.04f, 0.046f * q.Lip + 0.004f, Color.FromArgb(238, 245, 255));
        }
    }

    /// <summary>The point at the nose of a hare pose, in units (for a puff of breath).</summary>
    public static PointF HareNoseOf(HPose q) => Add(q.Head, Rot(P(0.15f * 1.30f + q.Nose, -0.15f * 0.12f), q.HeadTilt));

    // ---------------------------------------------------------------- the hare's poses

    /// <summary>
    /// SIT, t = 0..1 the idle cycle (2.4 s): the nose twitches in two bursts, the far ear swivels and then the near
    /// one (one at a time, as a hare's do), a slow blink, a breath that lifts the chest. Never still.
    /// </summary>
    public static HPose HSit(float t)
    {
        var q = new HPose();
        float burst = t < 0.42f ? 1f : t > 0.62f && t < 0.86f ? 0.7f : 0.12f;
        q.Nose = 0.016f * MathF.Sin(MathF.Tau * 9f * t) * burst;
        float swL = t > 0.08f && t < 0.34f ? MathF.Sin((t - 0.08f) / 0.26f * MathF.Tau) : 0f;
        float swR = t > 0.52f && t < 0.78f ? MathF.Sin((t - 0.52f) / 0.26f * MathF.Tau) : 0f;
        q.EarL += 26f * swL; q.EarR += 22f * swR;
        q.Blink = t > 0.44f && t < 0.52f ? MathF.Sin((t - 0.44f) / 0.08f * MathF.PI) : t > 0.93f ? MathF.Sin((t - 0.93f) / 0.07f * MathF.PI) * 0.8f : 0f;
        float breath = MathF.Sin(MathF.Tau * t);
        q.Ry += 0.006f * breath; q.Head = P(q.Head.X, q.Head.Y + 0.004f * breath);
        q.HeadTilt = 3f * MathF.Sin(MathF.Tau * t + 1f);
        return q;
    }

    /// <summary>A hare sitting with its head up and ears pricked: how it looks at a noise or at Santa overhead. up 0..1 how high.</summary>
    public static HPose HLook(float up)
    {
        var q = HSit(0.2f);
        q.Head = P(0.37f + 0.02f * up, 0.53f + 0.05f * up); q.HeadTilt = 6f + 30f * up;
        q.EarL = 92f; q.EarR = 86f; q.Nose = 0.01f * up;
        q.Tilt = 30f + 4f * up;
        return q;
    }

    /// <summary>The hare reaching down to a twig: head low and forward, ears laid back, paws forward.</summary>
    public static HPose HReach()
    {
        var q = new HPose
        {
            Body = P(0.00f, 0.20f), Tilt = 14f, Head = P(0.50f, 0.17f), HeadTilt = -34f, EarL = 138f, EarR = 130f,
            ForeF = P(0.42f, 0f), ForeN = P(0.49f, 0f), ToeF = P(0.14f, 0f), ToeN = P(0.20f, 0f),
        };
        return q;
    }

    /// <summary>Nibbling at a twig: the jaw works, the nose twitches, the head bobs with each bite. t is one bite cycle.</summary>
    public static HPose HNibble(float t)
    {
        var q = HReach();
        float bite = MathF.Sin(MathF.Tau * t * 2f);
        q.Chew = 0.5f + 0.5f * bite; q.Nose = 0.013f * MathF.Sin(MathF.Tau * t * 5f);
        q.Head = P(q.Head.X + 0.012f * bite, q.Head.Y + 0.010f * bite); q.HeadTilt -= 4f * bite;
        q.EarL += 5f * MathF.Sin(MathF.Tau * t); q.Blink = t > 0.80f && t < 0.9f ? 0.7f : 0f;
        return q;
    }

    /// <summary>
    /// GROOM, a script on t = 0..1: it washes its face with both forepaws, stroking up and down (0 to 0.45), then
    /// reaches the paws up, pulls ONE EAR down to its face (0.45 to 0.58), licks along it with its eyes shut (0.58 to
    /// 0.88) and lets it spring back (0.88 to 1). Between poses the paws and ear are blended, so nothing jumps.
    /// </summary>
    public static HPose HGroom(float t)
    {
        var wash = HSit(0.05f);
        wash.Head = P(0.33f, 0.47f); wash.HeadTilt = -14f; wash.EarL = 104f; wash.EarR = 98f; wash.Blink = 0.85f;
        float stroke = MathF.Sin(MathF.Tau * 4f * t);
        wash.ForeF = P(0.44f, 0.455f + 0.06f * stroke); wash.ForeN = P(0.49f, 0.485f - 0.06f * stroke);
        var pull = HSit(0.05f);
        pull.Head = P(0.34f, 0.50f); pull.HeadTilt = 20f; pull.EarL = 96f; pull.EarR = 94f; pull.BendR = 1f; pull.Blink = 0.9f;
        EarGeom(pull, true, out _, out _, out PointF tip);
        pull.ForeN = P(tip.X + 0.01f, tip.Y - 0.01f); pull.ForeF = P(tip.X - 0.04f, tip.Y - 0.05f);
        if (t < 0.45f) return wash;
        if (t < 0.58f) return HLerp(wash, pull, Sm((t - 0.45f) / 0.13f));
        if (t < 0.88f)
        {
            float lick = MathF.Sin(MathF.Tau * 3f * (t - 0.58f) / 0.30f);
            var q = HLerp(pull, pull, 0f);
            q.Head = P(pull.Head.X + 0.016f * lick, pull.Head.Y - 0.012f * lick); q.HeadTilt = pull.HeadTilt - 5f * lick;
            EarGeom(q, true, out _, out _, out PointF tp);
            q.ForeN = P(tp.X + 0.01f, tp.Y - 0.01f); q.ForeF = P(tp.X - 0.04f, tp.Y - 0.05f);
            q.Chew = 0.5f + 0.5f * lick;
            return q;
        }
        return HLerp(pull, wash, Sm((t - 0.88f) / 0.12f));
    }

    /// <summary>STAND TALL, up on the hind legs like a meerkat: t cycles the idle (ear swivel, twitch); foot 0..1 is the thump (the near hind foot cocked up).</summary>
    public static HPose HTall(float t, float foot = 0f)
    {
        var q = new HPose
        {
            Body = P(-0.03f, 0.41f), Tilt = 72f + 5f * foot, Rx = 0.37f, Ry = 0.23f, Head = P(0.14f + 0.01f * foot, 0.93f + 0.04f * foot), HeadTilt = 12f,
            EarL = 100f, EarR = 92f, ForeF = P(0.30f, 0.50f), ForeN = P(0.35f, 0.46f),
            ToeF = P(0.16f, 0f), ToeN = P(0.24f - 0.06f * foot, 0.30f * foot), AngF = 0f, AngN = 22f * foot,
        };
        float swL = t > 0.10f && t < 0.40f ? MathF.Sin((t - 0.10f) / 0.30f * MathF.Tau) : 0f;
        float swR = t > 0.55f && t < 0.85f ? MathF.Sin((t - 0.55f) / 0.30f * MathF.Tau) : 0f;
        q.EarL += 20f * swL; q.EarR += 16f * swR;
        q.Nose = 0.012f * MathF.Sin(MathF.Tau * 7f * t);
        q.Blink = t > 0.46f && t < 0.52f ? 1f : 0f;
        q.HeadTilt += 3f * MathF.Sin(MathF.Tau * t);
        return q;
    }

    // ---- the lollop (and the bolt): a cycle paged by distance so the planted feet do not slide ----
    //
    // One cycle p 0..1, the body moving evenly forward by D (units) over it, so a planted foot is drawn
    // sliding BACK by exactly what the body moves (x relative = x planted minus D*p).
    //   forefeet: planted from p = 0.52 to 1.02 (they land first, ahead of the body, and are left behind),
    //   hind feet: planted from p = 0.88 to 1.14 (they swing through and land AHEAD of the forefeet, then push off),
    //   in the air between 0.14 and 0.52 both pairs are off the snow: the stretch.
    // All four feet are down together from 0.88 to 1.02: the gathered rest page (HopRest).
    public const float HopRest = 0.95f;

    static float WrapP(float p) => p - MathF.Floor(p);

    public static HPose HHop(float p, float D, bool flee)
    {
        p = WrapP(p);
        float stretch = flee ? 1.22f : 1f;
        var q = new HPose { ForeF = P(0f, 0f) };
        float foreLand = 0.46f * stretch, hindLand = 0.22f * stretch;
        float Ff = 0.52f * D + foreLand, Hh = 0.88f * D + hindLand;          // where (in the body's travelling frame at p = 0) each pair is planted
        // body height and attitude: low at the gather, springing up through the push, the peak in the air, down onto the forefeet
        float air = p > 0.14f && p < 0.52f ? MathF.Sin((p - 0.14f) / 0.38f * MathF.PI) : 0f;
        float h0 = 0.265f;
        q.Body = P(0f, h0 + (0.20f + (flee ? 0.03f : 0.1f)) * air + 0.03f * Sm((p - 0.0f) / 0.14f) * (p < 0.14f ? 1f : 0f));
        // pitch: nose up in the push, levelling in the air, nose down onto the forefeet, then the body comes through to crouch
        float pitch = p < 0.14f ? Lerp(26f, 32f, p / 0.14f) : p < 0.34f ? Lerp(32f, 8f, Sm((p - 0.14f) / 0.20f)) : p < 0.52f ? Lerp(8f, -14f, Sm((p - 0.34f) / 0.18f)) : p < 0.88f ? Lerp(-14f, 18f, Sm((p - 0.52f) / 0.36f)) : Lerp(18f, 26f, Sm((p - 0.88f) / 0.12f));
        q.Tilt = flee ? pitch * 0.5f : pitch;
        q.Rx = 0.36f * (1f + (flee ? 0.22f : 0.12f) * air); q.Ry = 0.25f * (1f - 0.10f * air);
        // head and ears
        float headUp = p > 0.14f && p < 0.60f ? 0.5f : 0f;
        q.Head = Add(q.Body, Rot(P(0.33f + 0.05f * air, 0.24f + 0.015f * air - 0.02f * headUp), q.Tilt * 0.5f));
        q.HeadTilt = q.Tilt * 0.5f;
        q.EarL = flee ? 150f : 104f + 34f * air - 12f * Sm((p - 0.52f) / 0.2f); q.EarR = q.EarL - 7f;
        if (!flee && (p < 0.14f || p > 0.60f)) { q.EarL = 98f; q.EarR = 90f; }
        // forefeet
        float fp = p < 0.02f ? p + 1f : p;
        if (fp >= 0.52f) q.ForeN = P(Ff - D * fp, 0f);                                           // planted: slides back by what the body moves
        else
        {
            float s = Math.Clamp((p - 0.02f) / 0.50f, 0f, 1f);
            q.ForeN = P(Lerp(Ff - D * 1.02f, Ff - D * 0.52f, Sm(s)), 0.16f * MathF.Sin(MathF.PI * MathF.Pow(s, 0.8f)));
        }
        q.ForeF = P(q.ForeN.X - 0.07f, q.ForeN.Y);
        // hind feet: toes planted from 0.88 to 1.14 (the heel rises in the push), swinging back, tucking, reaching forward in between
        float hp = p < 0.14f ? p + 1f : p;
        if (hp >= 0.88f)
        {
            q.ToeN = P(Hh - D * hp, 0f);
            q.AngN = hp > 1.02f ? 58f * Sm((hp - 1.02f) / 0.12f) : 0f;
        }
        else
        {
            float s = Math.Clamp((p - 0.14f) / 0.74f, 0f, 1f);
            float trail = Hh - D * 1.14f;                                                        // where the toe was when it left the snow
            float reach = Hh - D * 0.88f;                                                        // where it will land (relative to the body then)
            float x = s < 0.28f ? Lerp(trail, trail - 0.25f, Sm(s / 0.28f)) : s < 0.66f ? Lerp(trail - 0.25f, -0.02f, Sm((s - 0.28f) / 0.38f)) : Lerp(-0.02f, reach, Sm((s - 0.66f) / 0.34f));
            float y = (0.16f + 0.14f * air) * MathF.Sin(MathF.PI * MathF.Pow(s, 0.75f)) + 0.24f * air * (s < 0.5f ? 1f : 0f);
            q.ToeN = P(x, y);
            q.AngN = s < 0.4f ? Lerp(58f, 14f, Sm(s / 0.4f)) : Lerp(14f, 0f, Sm((s - 0.4f) / 0.6f));
        }
        q.ToeF = P(q.ToeN.X - 0.04f, q.ToeN.Y); q.AngF = q.AngN;
        q.Shadow = 1f - 0.5f * air;
        return q;
    }

    /// <summary>The gathered rest of a hop settling into a sit (and the reverse, to set off): s 0 the rest page, 1 the sit.</summary>
    public static HPose HSettle(float s, float D, bool flee) => HLerp(HHop(HopRest, D, flee), HSit(0.05f), Sm(s));

    /// <summary>
    /// A BINKY: the joyful leap of a happy hare. Crouch, spring straight up with the body stretching, at the top it
    /// TWISTS (the body turns toward us, the back arches, the hind feet kick out and up, the ears fly), then it drops
    /// and lands. t 0..1.
    /// </summary>
    public static HPose HBinky(float t)
    {
        var crouch = HSit(0.05f);
        crouch.Body = P(0f, 0.22f); crouch.Tilt = 20f; crouch.Head = P(0.37f, 0.47f); crouch.EarL = 120f; crouch.EarR = 116f;
        var launch = new HPose { Body = P(0.05f, 0.55f), Tilt = 48f, Rx = 0.40f, Ry = 0.22f, Head = P(0.46f, 0.88f), HeadTilt = 30f, EarL = 112f, EarR = 106f,
            ForeF = P(0.40f, 0.62f), ForeN = P(0.46f, 0.58f), ToeF = P(-0.30f, 0.12f), ToeN = P(-0.25f, 0.15f), AngF = 50f, AngN = 55f };
        var apex = new HPose { Body = P(0.05f, 0.98f), Tilt = 8f, Rx = 0.40f, Ry = 0.23f, Head = P(0.44f, 1.14f), HeadTilt = 10f, EarL = 150f, EarR = 138f, Yaw = 62f,
            ForeF = P(0.34f, 0.78f), ForeN = P(0.40f, 0.74f), ToeF = P(-0.52f, 1.12f), ToeN = P(-0.46f, 0.86f), AngF = 30f, AngN = 12f, Shadow = 0.5f };
        var drop = new HPose { Body = P(0.10f, 0.62f), Tilt = -18f, Rx = 0.38f, Ry = 0.24f, Head = P(0.48f, 0.60f), HeadTilt = -22f, EarL = 135f, EarR = 128f, Yaw = 20f,
            ForeF = P(0.50f, 0.18f), ForeN = P(0.56f, 0.14f), ToeF = P(-0.15f, 0.40f), ToeN = P(-0.10f, 0.36f), AngF = 30f, AngN = 30f, Shadow = 0.75f };
        var land = HHop(HopRest, 1.2f, false);
        if (t < 0.12f) return HLerp(HSit(0.05f), crouch, Sm(t / 0.12f));
        if (t < 0.28f) return HLerp(crouch, launch, Sm((t - 0.12f) / 0.16f));
        if (t < 0.52f) return HLerp(launch, apex, Sm((t - 0.28f) / 0.24f));
        if (t < 0.76f) return HLerp(apex, drop, Sm((t - 0.52f) / 0.24f));
        if (t < 0.88f) return HLerp(drop, land, Sm((t - 0.76f) / 0.12f));
        return HLerp(land, HSit(0.05f), Sm((t - 0.88f) / 0.12f));
    }

    /// <summary>A hare diving into a snow burrow: it leaps nose-first, the body turning over to point down, and the snow swallows it hind feet last. t 0..1.</summary>
    public static HPose HDive(float t)
    {
        var run = HHop(0.30f, 2.0f, true);
        var plunge = new HPose { Body = P(0.30f, 0.42f), Tilt = -72f, Rx = 0.38f, Ry = 0.23f, Head = P(0.50f, 0.04f), HeadTilt = -78f, EarL = 168f, EarR = 160f,
            ForeF = P(0.42f, -0.08f), ForeN = P(0.48f, -0.10f), ToeF = P(0.04f, 0.74f), ToeN = P(0.10f, 0.70f), AngF = 62f, AngN = 66f, Clip = true };
        float a = Sm(t / 0.5f);
        var q = HLerp(run, plunge, a);
        q.Clip = true;
        float sink = Sm((t - 0.45f) / 0.55f) * 1.35f;
        q.Sink = sink; q.Lip = Sm((t - 0.35f) / 0.4f);
        q.Shadow = 1f - Sm((t - 0.5f) / 0.4f);
        return q;
    }

    /// <summary>A hare coming up out of its burrow: only the ears at first, then the eyes, then the head. level 0 (ear tips) to 1 (head and shoulders); look swivels the ears.</summary>
    public static HPose HPeek(float level, float look)
    {
        var q = HSit(0.05f);
        q.Clip = true; q.Sink = Lerp(0.76f, 0.30f, level); q.Lip = 1f; q.Shadow = 0f;
        q.EarL = 96f + 24f * look; q.EarR = 90f - 18f * look;
        q.Head = P(0.37f, 0.53f);
        q.Blink = level > 0.2f && level < 0.45f ? 0f : 0f;
        return q;
    }

    /// <summary>The sat hare turned "yaw" degrees toward us: the pages that turn it round on the spot.</summary>
    public static HPose HTurn(float yaw)
    {
        var q = HSit(0.05f);
        q.Yaw = yaw; q.EarL = 100f; q.EarR = 82f;
        return q;
    }


    // ---------------------------------------------------------------- keyed sequences (the round-two sheets)
    //
    // FEYNMAN VERSION. A dive or a climb out is not one pose turned round: it is a RUN OF KEY POSES (crouch, push off,
    // fly, nose in, shoulders in, hind feet last) with the in-between pages drawn by a SMOOTH CURVE through them (a
    // Catmull-Rom spline, the way an animator's in-betweens ease through every key instead of stopping at each).
    // The sheet is anchored at the HOLE, not at the body: x = 0 is the middle of the burrow, y = 0 the snow line,
    // and the hare's body travels across the page. The saver puts the page's origin on the hole.

    /// <summary>A pose moved sideways by dx (units): every point that is a place, not an angle.</summary>
    public static HPose HShift(HPose a, float dx)
    {
        var q = HLerp(a, a, 0f);
        q.Body = P(a.Body.X + dx, a.Body.Y); q.Head = P(a.Head.X + dx, a.Head.Y);
        q.ForeF = P(a.ForeF.X + dx, a.ForeF.Y); q.ForeN = P(a.ForeN.X + dx, a.ForeN.Y);
        q.ToeF = P(a.ToeF.X + dx, a.ToeF.Y); q.ToeN = P(a.ToeN.X + dx, a.ToeN.Y);
        return q;
    }

    /// <summary>Every number of a pose through the key poses by a smooth curve (t between ts[0] and ts[^1]).</summary>
    public static HPose HSpline(float t, float[] ts, HPose[] k)
    {
        float V(Func<HPose, float> f) { var vs = new float[k.Length]; for (int i = 0; i < k.Length; i++) vs[i] = f(k[i]); return Spline(t, ts, vs); }
        return new HPose
        {
            Body = P(V(a => a.Body.X), V(a => a.Body.Y)), Rx = V(a => a.Rx), Ry = V(a => a.Ry), Tilt = V(a => a.Tilt),
            Head = P(V(a => a.Head.X), V(a => a.Head.Y)), HeadTilt = V(a => a.HeadTilt), EarL = V(a => a.EarL), EarR = V(a => a.EarR),
            BendL = V(a => a.BendL), BendR = V(a => a.BendR),
            ForeF = P(V(a => a.ForeF.X), V(a => a.ForeF.Y)), ForeN = P(V(a => a.ForeN.X), V(a => a.ForeN.Y)),
            ToeF = P(V(a => a.ToeF.X), V(a => a.ToeF.Y)), ToeN = P(V(a => a.ToeN.X), V(a => a.ToeN.Y)),
            AngF = V(a => a.AngF), AngN = V(a => a.AngN), Nose = V(a => a.Nose), Yaw = V(a => a.Yaw), Sink = V(a => a.Sink),
            Blink = V(a => a.Blink), Chew = V(a => a.Chew), Shadow = V(a => a.Shadow), Lip = V(a => a.Lip), LipX = 0f,
        };
    }

    /// <summary>Where the dive's first page puts the body, behind the hole (units). The saver anchors the hole this far ahead of the running hare.</summary>
    public const float DiveRun = 0.55f;

    /// <summary>THE DIVE, t 0..1 (1.0 s): the gather at the end of a bolt, the push off the long hind feet, a flat flying stretch, the nose and forelegs into the snow, the body sliding in along its own line, the hind feet and the white tail last.</summary>
    public static HPose HDive2(float t)
    {
        float[] ts = [0f, 0.20f, 0.42f, 0.62f, 0.80f, 0.93f, 1f];
        HPose[] k =
        [
            HShift(HHop(15f / 16f, HareFleeD, true), -DiveRun),
            new HPose { Body = P(-0.34f, 0.34f), Tilt = 14f, Rx = 0.42f, Ry = 0.22f, Head = P(0.06f, 0.52f), HeadTilt = 8f, EarL = 156f, EarR = 150f, ForeF = P(0.14f, 0.30f), ForeN = P(0.21f, 0.27f), ToeF = P(-0.86f, 0.05f), ToeN = P(-0.79f, 0.01f), AngF = 52f, AngN = 56f, Shadow = 0.9f },
            new HPose { Body = P(-0.02f, 0.50f), Tilt = -14f, Rx = 0.42f, Ry = 0.21f, Head = P(0.42f, 0.42f), HeadTilt = -36f, EarL = 166f, EarR = 160f, ForeF = P(0.60f, 0.20f), ForeN = P(0.66f, 0.16f), ToeF = P(-0.50f, 0.46f), ToeN = P(-0.44f, 0.40f), AngF = 40f, AngN = 46f, Shadow = 0.55f },
            new HPose { Body = P(0.22f, 0.30f), Tilt = -48f, Rx = 0.40f, Ry = 0.22f, Head = P(0.50f, -0.04f), HeadTilt = -68f, EarL = 172f, EarR = 166f, ForeF = P(0.62f, -0.20f), ForeN = P(0.68f, -0.24f), ToeF = P(-0.03f, 0.64f), ToeN = P(0.04f, 0.60f), AngF = 60f, AngN = 64f, Shadow = 0.3f, Lip = 0.6f },
            new HPose { Body = P(0.34f, 0.02f), Tilt = -68f, Rx = 0.38f, Ry = 0.23f, Head = P(0.50f, -0.40f), HeadTilt = -78f, EarL = 174f, EarR = 168f, ForeF = P(0.55f, -0.52f), ForeN = P(0.60f, -0.56f), ToeF = P(0.14f, 0.52f), ToeN = P(0.20f, 0.48f), AngF = 64f, AngN = 66f, Shadow = 0.05f, Lip = 1f },
            new HPose { Body = P(0.38f, -0.30f), Tilt = -76f, Rx = 0.38f, Ry = 0.23f, Head = P(0.47f, -0.72f), HeadTilt = -80f, EarL = 174f, EarR = 168f, ForeF = P(0.52f, -0.8f), ForeN = P(0.56f, -0.84f), ToeF = P(0.25f, 0.20f), ToeN = P(0.31f, 0.16f), AngF = 66f, AngN = 68f, Shadow = 0f, Lip = 1f },
            new HPose { Body = P(0.40f, -0.60f), Tilt = -78f, Rx = 0.38f, Ry = 0.23f, Head = P(0.47f, -1.0f), HeadTilt = -80f, EarL = 174f, EarR = 168f, ForeF = P(0.52f, -1.1f), ForeN = P(0.56f, -1.14f), ToeF = P(0.30f, -0.10f), ToeN = P(0.36f, -0.14f), AngF = 66f, AngN = 68f, Shadow = 0f, Lip = 1f },
        ];
        var q = HSpline(t, ts, k);
        q.Clip = t > 0.52f; q.Lip = Sm((t - 0.50f) / 0.30f); q.Shadow = Math.Clamp(q.Shadow, 0f, 1f); q.LipX = 0.02f;
        return q;
    }

    /// <summary>
    /// COMING UP, t 0..1 (2.4 s), in the reverse order of the dive's body parts: the EAR TIPS (they swivel), the eyes, the
    /// head (a look round), the shoulders, then a hop OUT onto the snow and a settle into a sit, 0.85 ahead of the hole.
    /// </summary>
    public static HPose HEmerge(float t)
    {
        HPose Up(float sink, float earL, float earR, float headTilt)
        {
            var q = HSit(0.05f); q.Sink = sink; q.EarL = earL; q.EarR = earR; q.HeadTilt = headTilt; q.Lip = 1f; q.Clip = true; q.Shadow = 0f;
            return q;
        }
        var launch = HLerp(HSit(0.05f), HHop(0.02f, HareHopD, false), 0.55f);       // the push: the gather, hind toes still down
        var fly = new HPose { Body = P(0.48f, 0.52f), Tilt = 10f, Rx = 0.38f, Ry = 0.22f, Head = P(0.84f, 0.64f), HeadTilt = 6f, EarL = 118f, EarR = 112f, ForeF = P(0.74f, 0.40f), ForeN = P(0.80f, 0.36f), ToeF = P(0.08f, 0.40f), ToeN = P(0.14f, 0.36f), AngF = 40f, AngN = 44f, Shadow = 0.5f };
        float[] ts = [0f, 0.12f, 0.22f, 0.36f, 0.48f, 0.58f, 0.66f, 0.74f, 0.82f, 0.88f, 1f];
        HPose[] k =
        [
            Up(0.84f, 96f, 90f, 3f), Up(0.76f, 120f, 78f, 3f), Up(0.66f, 88f, 106f, 3f), Up(0.52f, 94f, 90f, 0f),
            Up(0.42f, 100f, 96f, -6f), Up(0.32f, 100f, 96f, 9f), Up(0.20f, 98f, 92f, 3f),
            HShift(launch, 0.02f), fly, HShift(HHop(HopRest, HareHopD, false), 0.85f), HShift(HSit(0.05f), 0.85f),
        ];
        // the spline blends Clip and Lip badly across the exit from the hole: those are set by hand
        var q = HSpline(t, ts, k);
        q.Clip = t < 0.70f; q.Lip = t < 0.66f ? 1f : 1f - Sm((t - 0.66f) / 0.30f); q.Sink = t < 0.66f ? q.Sink : 0f; q.LipX = 0.02f;
        q.Shadow = t < 0.74f ? 0f : Sm((t - 0.74f) / 0.26f);
        return q;
    }

    /// <summary>
    /// GROOM as a SHEET of loops (the first one was one slow pass of 24 pages: four face strokes were sampled by
    /// ten pages, a strobe). Pages: 0..3 lead-in, 4..15 the wash loop (one stroke cycle), 16..21 up to the ear,
    /// 22..33 the lick loop, 34..39 the release to a sit, 40..44 a quick way out of a wash to a sit.
    /// </summary>
    public const int GrLead = 0, GrWash = 8, GrToPull = 20, GrLick = 28, GrRelease = 40, GrOut = 48;
    static HPose GroomWash(float s)
    {
        var wash = HSit(0.05f);
        wash.Head = P(0.33f, 0.47f); wash.HeadTilt = -14f; wash.EarL = 104f; wash.EarR = 98f; wash.Blink = 0.85f;
        float stroke = MathF.Sin(MathF.Tau * s);
        wash.ForeF = P(0.44f, 0.455f + 0.06f * stroke); wash.ForeN = P(0.49f, 0.485f - 0.06f * stroke);
        return wash;
    }
    static HPose GroomPull()
    {
        var pull = HSit(0.05f);
        pull.Head = P(0.34f, 0.50f); pull.HeadTilt = 20f; pull.EarL = 96f; pull.EarR = 94f; pull.BendR = 1f; pull.Blink = 0.9f;
        EarGeom(pull, true, out _, out _, out PointF tip);
        pull.ForeN = P(tip.X + 0.01f, tip.Y - 0.01f); pull.ForeF = P(tip.X - 0.04f, tip.Y - 0.05f);
        return pull;
    }
    static HPose GroomLick(float s)
    {
        var pull = GroomPull();
        float lick = MathF.Sin(MathF.Tau * s);
        var q = HLerp(pull, pull, 0f);
        q.Head = P(pull.Head.X + 0.016f * lick, pull.Head.Y - 0.012f * lick); q.HeadTilt = pull.HeadTilt - 5f * lick;
        EarGeom(q, true, out _, out _, out PointF tp);
        q.ForeN = P(tp.X + 0.01f, tp.Y - 0.01f); q.ForeF = P(tp.X - 0.04f, tp.Y - 0.05f);
        q.Chew = 0.5f + 0.5f * lick;
        return q;
    }
    public static HPose HGroom2(int i)
    {
        if (i < GrWash) return HLerp(HSit(0.05f), GroomWash(0f), Sm((i + 1) / 8f));
        if (i < GrToPull) return GroomWash((i - GrWash) / 12f);
        if (i < GrLick) return HLerp(GroomWash(0f), GroomLick(0f), Sm((i - GrToPull + 1) / 8f));
        if (i < GrRelease) return GroomLick((i - GrLick) / 12f);
        if (i < GrOut) return HLerp(GroomLick(0f), HSit(0.05f), Sm((i - GrRelease + 1) / 8f));
        return HLerp(GroomWash(0f), HSit(0.05f), Sm((i - GrOut + 1) / 6f));
    }

    /// <summary>The head up from a nibble, ears pricked, still crouched at the twig: k 0 (the nibble) to 1.</summary>
    public static HPose HNibbleUp(float k, float t = 0f)
    {
        var up = HReach();
        up.Head = P(0.46f, 0.34f); up.HeadTilt = 10f; up.EarL = 104f + 8f * MathF.Sin(MathF.Tau * t); up.EarR = 96f + 6f * MathF.Sin(MathF.Tau * t + 2f);
        up.Nose = 0.012f * MathF.Sin(MathF.Tau * 3f * t); up.Tilt = 20f;
        return HLerp(HNibble(0f), up, Sm(k));
    }

    /// <summary>The body points the mind log uses to measure a pose pop: head, body, the near ear's tip, both fore paws, both hind toes.</summary>
    public static PointF[] HKeys(HPose q)
    {
        EarGeom(q, true, out _, out _, out PointF tip);
        return [q.Head, q.Body, tip, q.ForeN, q.ForeF, q.ToeN, q.ToeF];
    }

    // ---------------------------------------------------------------- the hare's twig and the snow decals

    /// <summary>A bare twig poking up through the snow, with two side shoots and a few buds: what a hare nibbles. Painted in hare units; origin at its foot.</summary>
    public static void PaintTwig(Graphics g)
    {
        Color wood = Color.FromArgb(120, 84, 62), bud = Color.FromArgb(168, 112, 84);
        Ell(g, 0f, 0f, 0.07f, 0.02f, Color.FromArgb(70, 70, 100, 170));
        void Shoot(PointF a, PointF b, float w) => Limb(g, a, b, w, wood);
        Shoot(P(0f, 0f), P(0.01f, 0.18f), 0.014f); Shoot(P(0.01f, 0.18f), P(0.00f, 0.34f), 0.010f);
        Shoot(P(0.008f, 0.14f), P(0.09f, 0.27f), 0.009f); Shoot(P(0.004f, 0.23f), P(-0.07f, 0.36f), 0.008f);
        foreach (var b in new[] { P(0.00f, 0.345f), P(0.09f, 0.275f), P(-0.07f, 0.365f), P(0.05f, 0.21f) }) Ell(g, b, 0.012f, 0.016f, bud);
        Ell(g, 0f, 0.005f, 0.05f, 0.026f, Color.FromArgb(238, 245, 255));        // a little snow heaped round the foot
    }

    // ---------------------------------------------------------------- the hare's books

    /// <summary>The hare's flip-books, in the order the resident indexes them.</summary>
    public static BookSpec[] HareBooks()
    {
        const float W = 1.25f;
        BookSpec Make(string name, int n, float halfW, float above, Func<int, HPose> pose) => new()
        {
            Name = name, N = n, HalfW = halfW, Above = above,
            Paint = (g, i) => PaintHare(g, pose(i)),
            Aux = i => HareNoseOf(pose(i)),
            Keys = i => HKeys(pose(i)),
        };
        float D = HareHopD, DF = HareFleeD;
        return
        [
            Make("sit", 24, 0.8f, 1.1f, i => HSit(i / 24f)),                                   // 0
            Make("look", 9, 0.8f, 1.2f, i => HLook(i / 8f)),                                    // 1  the head up by eighths: it can FOLLOW a sleigh
            Make("reach", 6, 0.9f, 1.1f, i => HLerp(HSit(0.05f), HReach(), Sm(i / 5f))),         // 2  sit to the twig (page 0 IS the sit page)
            Make("nibble", 12, 0.9f, 1.1f, i => HNibble(i / 12f)),                              // 3
            Make("groom", 54, 0.8f, 1.1f, HGroom2),                                             // 4  loops: see GrWash and friends
            Make("hop", 24, W, 1.3f, i => HHop(i / 24f, D, false)),                             // 5
            Make("flee", 16, W + 0.4f, 1.3f, i => HHop(i / 16f, DF, true)),                     // 6
            Make("settle", 6, W, 1.2f, i => HSettle((i + 1) / 6f, D, false)),                   // 7  hop rest to sit
            Make("tallIn", 6, 0.8f, 1.5f, i => HLerp(HSit(0.05f), HTall(0f), Sm(i / 5f))),      // 8  page 0 IS the sit page
            Make("tall", 12, 0.8f, 1.5f, i => HTall(i / 12f)),                                  // 9
            Make("thump", 12, 0.8f, 1.5f, ThumpPose),                                           // 10
            Make("binky", 32, W, 1.75f, i => HBinky((i + 0.5f) / 32f)),                         // 11
            Make("dive", 30, 1.5f, 1.4f, i => HDive2(i / 29f)),                                 // 12 anchored at the HOLE (0 = the burrow's middle)
            Make("peek", 56, 1.5f, 1.4f, i => HEmerge(i / 55f)),                                // 13 the way out, anchored at the hole
            Make("turn", 4, 0.8f, 1.1f, i => HTurn(22.5f * (i + 1))),                           // 14 yaw 22.5, 45, 67.5, 90
            new BookSpec { Name = "twig", N = 1, HalfW = 0.25f, Above = 0.5f, Paint = (g, i) => PaintTwig(g) },   // 15
            Make("nlook", 14, 0.9f, 1.1f, i => i < 6 ? HNibbleUp((i + 1) / 6f) : HNibbleUp(1f, (i - 6) / 8f)),  // 16 head up from a twig: 6 pages up, then an 8 page hold
            Make("settleF", 8, W + 0.4f, 1.2f, i => HSettle((i + 1) / 8f, DF, true)),             // 17 the bolt's rest page to a sit
        ];
    }

    public const float HareHopD = 1.25f, HareFleeD = 2.3f;       // units a cycle of the lollop and of the bolt

    /// <summary>The thump, page i of 12: the near hind foot cocked up and driven down, twice (strikes at pages 4 and 9).</summary>
    static HPose ThumpPose(int i)
    {
        float[] f = [0f, 0.4f, 0.9f, 1f, 0f, 0f, 0.5f, 1f, 1f, 0f, 0f, 0f];
        return HTall(0.1f, f[i % 12]);
    }
}
