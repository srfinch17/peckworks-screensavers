using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using Peckworks.Screensavers.Core;

namespace Christmas;

// ============================================================================
// THE FOX (part two of FoxAndHaresArt: the same static class, split for size).
//
// A RED FOX, looked up before drawn (unit = ONE METRE, so the numbers read as
// real sizes):
//   - 0.40 m at the shoulder, body (chest to rump) about 0.55 long and slim,
//     belly at 0.18 m; the head 0.19 long with a pointed muzzle (a dog's, not a
//     cat's), big upright ears 0.10 tall, BLACK on the back; the white lower
//     jaw, cheeks and throat run down into a white chest;
//   - the BRUSH, 0.40 long and thick in the middle, rust with a WHITE TIP;
//   - legs slim and BLACK from the elbow and the hock down ("stockings"); the
//     hind hock bends BACKWARD (the leg stands on the toes);
//   - the gait: a TROT in diagonal pairs (near fore with far hind, then the
//     other two), the brush streaming level; a GALLOP when it runs flat out.
// Coat: rust (196, 92, 40) with a darker back and a thin moonlit rim along the
// spine, a cream belly and chest. It is on pale snow, so the rust and the black
// read well; the white parts get a faint blue-grey tone so they do not vanish.
// ============================================================================

internal static partial class FoxAndHaresArt
{
    public static readonly Color FoxCoat = Color.FromArgb(200, 94, 40);
    static readonly Color FoxBack = Color.FromArgb(170, 72, 32);          // the darker back
    static readonly Color FoxFar = Color.FromArgb(142, 62, 30);           // the far side's limbs
    static readonly Color FoxRim = Color.FromArgb(150, 244, 168, 118);    // moonlight along the spine
    static readonly Color FoxCream = Color.FromArgb(247, 238, 224);
    static readonly Color FoxCreamShade = Color.FromArgb(204, 206, 224);  // the white parts' blue-grey underside
    static readonly Color Sock = Color.FromArgb(36, 28, 30);
    static readonly Color FoxEye = Color.FromArgb(30, 22, 22);

    /// <summary>One fox pose (units: metres).</summary>
    public sealed class FPose
    {
        public PointF Body = P(0f, 0.335f);
        public float Rx = 0.29f, Ry = 0.105f, Pitch;          // the body ellipse; Pitch degrees nose-up
        public float NeckLen = 0.19f, NeckAng = 40f, HeadAng = -6f;   // the neck's and the head's WORLD angles, degrees (0 level, plus up)
        public float EarF = 6f, EarN = 6f;                    // far and near ear: degrees forward of straight up
        public float Mouth, Blink, Snow;                      // jaw open, eye shut, snow on the muzzle (all 0..1)
        public PointF FootFF = P(0.22f, 0f), FootFN = P(0.28f, 0f), FootHF = P(-0.21f, 0f), FootHN = P(-0.15f, 0f);   // paws; y is the lift
        public float HindFlat;                                // 1: the hind foot lies flat (a sitting or lying fox)
        public float TailAng = -4f, TailCurl = 10f, TailWrap, TailLen = 0.46f;
        public int WrapKind;                                  // 0 sitting (round the feet), 1 curled (over the nose)
        public float Tuck;                                    // 1: the legs are folded away (lying curled)
        public bool HeadFlip;                                 // the head turned upside down (the curl)
        public float BackSnow;                                // snow settled on the back, 0..1
        public float Yaw, Sink, Shadow = 1f, Lip;
        public bool Clip;
    }

    public static FPose FLerp(FPose a, FPose b, float t) => new()
    {
        Body = Lerp(a.Body, b.Body, t), Rx = Lerp(a.Rx, b.Rx, t), Ry = Lerp(a.Ry, b.Ry, t), Pitch = Lerp(a.Pitch, b.Pitch, t),
        NeckLen = Lerp(a.NeckLen, b.NeckLen, t), NeckAng = Lerp(a.NeckAng, b.NeckAng, t), HeadAng = Lerp(a.HeadAng, b.HeadAng, t),
        EarF = Lerp(a.EarF, b.EarF, t), EarN = Lerp(a.EarN, b.EarN, t), Mouth = Lerp(a.Mouth, b.Mouth, t), Blink = Lerp(a.Blink, b.Blink, t), Snow = Lerp(a.Snow, b.Snow, t),
        FootFF = Lerp(a.FootFF, b.FootFF, t), FootFN = Lerp(a.FootFN, b.FootFN, t), FootHF = Lerp(a.FootHF, b.FootHF, t), FootHN = Lerp(a.FootHN, b.FootHN, t),
        HindFlat = Lerp(a.HindFlat, b.HindFlat, t), TailAng = Lerp(a.TailAng, b.TailAng, t), TailCurl = Lerp(a.TailCurl, b.TailCurl, t),
        TailWrap = Lerp(a.TailWrap, b.TailWrap, t), TailLen = Lerp(a.TailLen, b.TailLen, t), WrapKind = t < 0.5f ? a.WrapKind : b.WrapKind,
        Tuck = Lerp(a.Tuck, b.Tuck, t), HeadFlip = t < 0.5f ? a.HeadFlip : b.HeadFlip, BackSnow = Lerp(a.BackSnow, b.BackSnow, t),
        Yaw = Lerp(a.Yaw, b.Yaw, t), Sink = Lerp(a.Sink, b.Sink, t), Shadow = Lerp(a.Shadow, b.Shadow, t), Lip = Lerp(a.Lip, b.Lip, t), Clip = a.Clip || b.Clip,
    };

    // the tail's two resting shapes, in pose units (nine points from the root to the tip)
    static readonly PointF[] WrapSit = [P(-0.22f, 0.10f), P(-0.27f, 0.055f), P(-0.22f, 0.025f), P(-0.12f, 0.022f), P(0.00f, 0.030f), P(0.11f, 0.038f), P(0.21f, 0.046f), P(0.29f, 0.060f), P(0.35f, 0.085f)];
    static readonly PointF[] WrapCurl = [P(-0.31f, 0.11f), P(-0.32f, 0.18f), P(-0.24f, 0.225f), P(-0.10f, 0.245f), P(0.04f, 0.240f), P(0.17f, 0.205f), P(0.26f, 0.150f), P(0.29f, 0.095f), P(0.27f, 0.050f)];

    public static void PaintFox(Graphics g, FPose q)
    {
        float yaw = q.Yaw * MathF.PI / 180f, c = MathF.Cos(yaw), s = MathF.Sin(yaw);
        PointF Pr(PointF p, float lat = 0f) => P(p.X * c + lat * s, p.Y);
        Shadow(g, 0.04f, 0.58f * (0.55f + 0.45f * c), 0.085f, q.Shadow);
        if (q.Clip) g.SetClip(new RectangleF(-4f, 0f, 8f, 6f));
        g.TranslateTransform(0f, -q.Sink);

        float tilt = q.Pitch * c;
        float brx = MathF.Sqrt(q.Rx * c * q.Rx * c + 0.12f * s * 0.12f * s);
        PointF N0 = Add(q.Body, Rot(P(q.Rx * 0.74f, q.Ry * 0.28f), q.Pitch));
        float na = q.NeckAng * MathF.PI / 180f;
        PointF Hc = P(N0.X + q.NeckLen * MathF.Cos(na), N0.Y + q.NeckLen * MathF.Sin(na));
        PointF shoulder = Add(q.Body, Rot(P(q.Rx * 0.68f, -q.Ry * 0.35f), q.Pitch));
        PointF hip = Add(q.Body, Rot(P(-q.Rx * 0.72f, -q.Ry * 0.25f), q.Pitch));

        // ---- the brush: nine points along a curve; the last quarter is white ----
        PointF[] spine = new PointF[9];
        {
            PointF root = Add(q.Body, Rot(P(-q.Rx * 0.90f, q.Ry * 0.12f), q.Pitch));
            float ph = (180f - q.TailAng) * MathF.PI / 180f, seg = q.TailLen / 8f;
            PointF cur = root;
            for (int i = 0; i < 9; i++)
            {
                spine[i] = cur;
                cur = P(cur.X + seg * MathF.Cos(ph), cur.Y + seg * MathF.Sin(ph));
                ph -= q.TailCurl / 8f * MathF.PI / 180f;
            }
            if (q.TailWrap > 0.001f)
            {
                PointF[] w = q.WrapKind == 0 ? WrapSit : WrapCurl;
                for (int i = 0; i < 9; i++) spine[i] = Lerp(spine[i], w[i], q.TailWrap);
            }
            for (int i = 0; i < 9; i++) spine[i] = Pr(spine[i], 0.03f);
        }
        void Strip(int i0, int i1, Color col)
        {
            // a band along the spine, thick in the middle of the brush and pointed at the tip
            var top = new List<PointF>(); var bot = new List<PointF>();
            for (int i = i0; i <= i1; i++)
            {
                PointF a = spine[Math.Max(0, i - 1)], b = spine[Math.Min(8, i + 1)];
                float dx = b.X - a.X, dy = b.Y - a.Y, len = MathF.Sqrt(dx * dx + dy * dy);
                if (len < 1e-5f) { dx = -1f; dy = 0f; len = 1f; }
                float nx = -dy / len, ny = dx / len, t = i / 8f;
                float w = 0.088f * MathF.Pow(MathF.Max(0f, MathF.Sin(MathF.PI * (0.10f + 0.90f * t))), 0.75f) * (q.TailLen / 0.46f) * (0.45f + 0.55f * c) * (q.TailWrap > 0.5f && q.WrapKind == 1 ? 0.78f : 1f);
                top.Add(P(spine[i].X + nx * w, spine[i].Y + ny * w)); bot.Add(P(spine[i].X - nx * w, spine[i].Y - ny * w));
            }
            bot.Reverse(); top.AddRange(bot);
            Blob(g, col, top.ToArray());
        }
        void Brush()
        {
            Strip(0, 8, FoxCoat);
            Strip(1, 7, Mix(FoxCoat, FoxBack, 0.45f));
            Strip(6, 8, FoxCream);
        }
        bool tailFront = q.TailWrap > 0.5f;
        if (!tailFront) Brush();

        // ---- legs ----
        void Fore(PointF paw, float lat, Color upper)
        {
            if (q.Tuck > 0.5f) { Ell(g, Pr(paw, lat).X, Pr(paw, lat).Y + 0.02f, 0.045f, 0.03f, Sock); return; }
            PointF wrist = P(paw.X - 0.004f, paw.Y + 0.075f);
            PointF sp = Pr(shoulder, lat), wp = Pr(wrist, lat), pp = Pr(paw, lat);
            PointF el = Knee(sp, wp, 0.125f, 0.125f, false);
            Limb(g, sp, el, 0.068f, upper);
            Limb(g, el, wp, 0.040f, Sock);
            Limb(g, wp, pp, 0.036f, Sock);
            Ell(g, pp.X + 0.012f, pp.Y + 0.014f, 0.034f, 0.022f, Sock);
        }
        void Hind(PointF paw, float lat, Color upper)
        {
            if (q.Tuck > 0.5f) { Ell(g, Pr(paw, lat).X, Pr(paw, lat).Y + 0.02f, 0.05f, 0.03f, Sock); return; }
            PointF hock = P(paw.X - Lerp(0.045f, 0.13f, q.HindFlat), paw.Y + Lerp(0.135f, 0.022f, q.HindFlat));
            PointF hp = Pr(hip, lat), kp = Pr(hock, lat), pp = Pr(paw, lat);
            PointF st = Knee(hp, kp, 0.14f, 0.14f, true);
            EllSeg(g, hp, st, 0.125f, upper, 0.02f);              // the thigh
            Limb(g, st, kp, 0.048f, upper);
            Limb(g, kp, pp, 0.036f, Sock);
            Ell(g, pp.X + 0.014f, pp.Y + 0.012f, 0.036f, 0.021f, Sock);
        }
        Fore(q.FootFF, -0.07f, FoxFar);
        Hind(q.FootHF, -0.07f, FoxFar);

        // ---- body ----
        Ell(g, Pr(q.Body), brx, q.Ry, FoxCoat, tilt);
        Ell(g, Pr(Add(q.Body, Rot(P(-0.01f, q.Ry * 0.40f), q.Pitch))), brx * 0.94f, q.Ry * 0.55f, FoxBack, tilt);          // the darker back
        Ell(g, Pr(Add(q.Body, Rot(P(0.03f, -q.Ry * 0.55f), q.Pitch))), brx * 0.80f, q.Ry * 0.46f, Mix(FoxCoat, FoxCream, 0.55f), tilt);   // the pale belly
        Ell(g, Pr(Add(q.Body, Rot(P(q.Rx * 0.62f, -q.Ry * 0.05f), q.Pitch))), q.Rx * 0.34f * (0.4f + 0.6f * c), q.Ry * 1.12f, FoxCoat, tilt);   // the deeper chest
        {
            // the moonlit rim: a thin pale arc along the top of the back (a tone, not a pen)
            var st = g.Save();
            PointF bc = Pr(q.Body);
            g.TranslateTransform(bc.X, bc.Y); g.RotateTransform(tilt);
            using var pen = new Pen(FoxRim, 0.014f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawArc(pen, -brx * 0.97f, -q.Ry * 0.97f, brx * 1.94f, q.Ry * 1.94f, 205f, 130f);     // GDI arcs go clockwise in y-down degrees; with the flipped y this is the upper side
            g.Restore(st);
        }
        if (q.BackSnow > 0.02f)
        {
            for (int i = 0; i < 6; i++)
            {
                float f = -0.7f + 0.28f * i;
                PointF at = Add(q.Body, Rot(P(q.Rx * f, q.Ry * 0.78f), q.Pitch));
                float sz = q.BackSnow * (0.045f + 0.02f * (i % 3));
                Ell(g, Pr(at).X, Pr(at).Y, sz * 1.6f, sz * 0.8f, Color.FromArgb(240, 242, 249, 255));
            }
        }

        // ---- the neck and white throat ----
        PointF nb = Pr(Add(q.Body, Rot(P(q.Rx * 0.50f, q.Ry * 0.15f), q.Pitch)));
        Limb(g, nb, Pr(Hc), 0.150f, FoxCoat);
        PointF mid = Lerp(Pr(N0), Pr(Hc), 0.55f);
        Ell(g, mid.X + 0.02f * c, mid.Y - 0.055f, 0.105f * (0.5f + 0.5f * c), 0.058f, FoxCream, q.NeckAng * 0.7f);
        if (q.TailWrap < 0.5f || true) { }

        // ---- the head: its own little frame about the neck's end ----
        float flip = q.HeadFlip ? -1f : 1f;
        const float HS = 1.2f;   // the head is a little big: storybook cuteness
        PointF HL(float f, float y) => Add(Hc, Rot(P(f * HS, y * flip * HS), q.HeadAng));
        PointF HP(float f, float y, float lat = 0f) => Pr(HL(f, y), lat);
        void Ear(float angle, bool near)
        {
            PointF b0 = HL(-0.040f, 0.050f);
            PointF[] tri = [P(-0.040f, 0f), P(0.040f, 0f), P(0.002f, 0.125f)];
            var pts = new PointF[3];
            for (int i = 0; i < 3; i++)
            {
                PointF r = Rot(tri[i], -angle);
                PointF w = P(b0.X + Rot(P(r.X, r.Y * flip), q.HeadAng).X, b0.Y + Rot(P(r.X, r.Y * flip), q.HeadAng).Y);
                pts[i] = Pr(w, near ? 0.06f : -0.06f);
            }
            Poly(g, near ? Sock : Color.FromArgb(28, 20, 22), pts);
            if (near)
            {
                PointF cen = P((pts[0].X + pts[1].X + pts[2].X) / 3f, (pts[0].Y + pts[1].Y + pts[2].Y) / 3f);
                Poly(g, Color.FromArgb(120, 150, 90, 70), Lerp(pts[0], cen, 0.35f), Lerp(pts[1], cen, 0.35f), Lerp(pts[2], cen, 0.30f));
            }
        }
        Ear(q.EarF, false);
        float hw = (0.086f * (0.45f + 0.55f * c) + 0.04f * s) * HS;
        Ell(g, HP(0f, 0f), hw, 0.071f * HS, FoxCoat, (q.HeadAng) * c * flip);
        // the muzzle, a tapered shape; the jaw below it hinges open
        {
            float jaw = q.Mouth * 28f;
            PointF[] upper = [HP(0.03f, 0.048f), HP(0.10f, 0.036f), HP(0.17f, 0.014f), HP(0.19f, -0.004f), HP(0.15f, -0.020f), HP(0.09f, -0.030f), HP(0.02f, -0.040f)];
            PointF JawPt(float f, float y) { PointF d = Rot(P(f - 0.01f, y + 0.03f), -jaw); return HP(d.X + 0.01f, d.Y - 0.03f); }
            if (q.Mouth > 0.08f) Poly(g, Color.FromArgb(120, 40, 44), HP(0.02f, -0.03f), HP(0.15f, -0.02f), JawPt(0.15f, -0.03f), JawPt(0.02f, -0.045f));
            Blob(g, FoxCoat, upper);
            Blob(g, FoxCream, JawPt(-0.01f, -0.035f), JawPt(0.09f, -0.036f), JawPt(0.165f, -0.026f), JawPt(0.12f, -0.062f), JawPt(0.03f, -0.080f), JawPt(-0.05f, -0.062f));
            Poly(g, FoxCream, HP(-0.060f, -0.015f), HP(-0.118f, -0.078f), HP(-0.025f, -0.082f));       // the white cheek ruff
            Poly(g, Color.FromArgb(60, FoxCreamShade), HP(-0.060f, -0.045f), HP(-0.118f, -0.078f), HP(-0.025f, -0.082f));
            Ell(g, HP(0.190f, 0.000f).X, HP(0.190f, 0.000f).Y, 0.021f, 0.016f, Sock);               // the black nose
            if (q.Snow > 0.02f)
            {
                int a = (int)(235 * Math.Clamp(q.Snow, 0f, 1f));
                Ell(g, HP(0.165f, -0.01f).X, HP(0.165f, -0.01f).Y, 0.055f * (0.4f + 0.6f * q.Snow), 0.040f * (0.4f + 0.6f * q.Snow), Color.FromArgb(a, 244, 248, 255));
                Ell(g, HP(0.04f, 0.055f).X, HP(0.04f, 0.055f).Y, 0.04f * q.Snow, 0.022f * q.Snow, Color.FromArgb(a, 244, 248, 255));
                Ell(g, HP(0.11f, 0.04f).X, HP(0.11f, 0.04f).Y, 0.025f * q.Snow, 0.018f * q.Snow, Color.FromArgb(a, 244, 248, 255));
            }
        }
        void Eye(float lat)
        {
            PointF e = HP(0.074f, 0.028f, lat);
            float ry = 0.0165f * (1f - 0.9f * q.Blink);
            Ell(g, e, 0.0185f, MathF.Max(0.0035f, ry), FoxEye);
            if (q.Blink < 0.5f) Ell(g, e.X + 0.006f, e.Y + 0.006f, 0.006f, 0.006f, Color.White);
        }
        if (q.Yaw > 8f) Eye(-0.045f);
        Eye(0.045f);
        Ear(q.EarN, true);

        // ---- the near legs, in front of the body ----
        Fore(q.FootFN, 0.07f, FoxCoat);
        Hind(q.FootHN, 0.07f, FoxCoat);
        if (tailFront) Brush();

        g.TranslateTransform(0f, q.Sink);
        if (q.Clip) g.ResetClip();
        if (q.Lip > 0.01f)
        {
            Ell(g, 0.16f, -0.012f, 0.30f * q.Lip + 0.04f, 0.050f * q.Lip + 0.004f, Color.FromArgb(205, 218, 244));
            Ell(g, 0.16f, 0.012f, 0.28f * q.Lip + 0.04f, 0.044f * q.Lip + 0.004f, Color.FromArgb(238, 245, 255));
        }
    }

    /// <summary>The fox's nose in pose units (where the breath comes from, where the snow is).</summary>
    public static PointF FoxNoseOf(FPose q)
    {
        PointF N0 = Add(q.Body, Rot(P(q.Rx * 0.74f, q.Ry * 0.28f), q.Pitch));
        float na = q.NeckAng * MathF.PI / 180f;
        PointF Hc = P(N0.X + q.NeckLen * MathF.Cos(na), N0.Y + q.NeckLen * MathF.Sin(na));
        float flip = q.HeadFlip ? -1f : 1f;
        return Add(Hc, Rot(P(0.195f, 0f), q.HeadAng));
    }

    // ---------------------------------------------------------------- the fox's poses

    /// <summary>A catmull-rom curve through values vs at times ts: a smooth path, not a row of straight bits.</summary>
    static float Spline(float t, float[] ts, float[] vs)
    {
        if (t <= ts[0]) return vs[0];
        if (t >= ts[^1]) return vs[^1];
        int i = 0; while (t > ts[i + 1]) i++;
        float u = (t - ts[i]) / (ts[i + 1] - ts[i]);
        float p0 = vs[Math.Max(0, i - 1)], p1 = vs[i], p2 = vs[i + 1], p3 = vs[Math.Min(vs.Length - 1, i + 2)];
        return 0.5f * (2 * p1 + (-p0 + p2) * u + (2 * p0 - 5 * p1 + 4 * p2 - p3) * u * u + (-p0 + 3 * p1 - 3 * p2 + p3) * u * u * u);
    }

    /// <summary>STAND, t = 0..1 an idle cycle (3 s): the chest breathes, an ear flicks, the brush sways, a slow blink, the head drifts.</summary>
    public static FPose FStand(float t)
    {
        var q = new FPose();
        float br = MathF.Sin(MathF.Tau * t * 2f);
        q.Ry += 0.004f * br; q.NeckAng += 1.5f * MathF.Sin(MathF.Tau * t + 0.4f); q.HeadAng += 2.5f * MathF.Sin(MathF.Tau * t + 1.1f);
        float fl = t > 0.30f && t < 0.42f ? MathF.Sin((t - 0.30f) / 0.12f * MathF.PI) : 0f;
        q.EarN += 20f * fl; q.EarF -= 8f * fl;
        q.Blink = t > 0.70f && t < 0.78f ? MathF.Sin((t - 0.70f) / 0.08f * MathF.PI) : 0f;
        q.TailAng = -8f + 6f * MathF.Sin(MathF.Tau * t); q.TailCurl = 14f + 8f * MathF.Sin(MathF.Tau * t + 0.8f);
        return q;
    }

    /// <summary>The head raised, ears forward, for a look at the sky or a noise: up 0 (a stand) to 1 (nose to the stars).</summary>
    public static FPose FLook(float up)
    {
        var q = FStand(0.1f);
        q.NeckAng = 40f + 28f * up; q.HeadAng = -6f + 44f * up; q.EarF = 6f + 14f * up; q.EarN = 6f + 14f * up;
        q.Pitch = 3f * up; q.Mouth = 0f;
        return q;
    }

    /// <summary>
    /// The TROT, phase 0..1 over one stride of "stride" metres. The legs go in diagonal pairs (near fore with far hind,
    /// then the other two). A foot on the snow slides back by exactly what the body moves (the page is chosen by
    /// DISTANCE walked, so it never skates); in the air it swings forward in an arc. "low" 0..1 crouches it into the
    /// stalk. Duty is the share of the stride a foot is down.
    /// </summary>
    public static FPose FTrot(float phase, float stride, float low = 0f, float duty = 0.55f)
    {
        phase = WrapP(phase);
        var q = new FPose();
        float reach = stride * duty / 2f, lift = Lerp(0.085f, 0.05f, low);
        PointF Foot(float u, float x0)
        {
            u = WrapP(u);
            if (u < duty) return P(x0 + reach - 2f * reach * u / duty, 0f);
            float s = (u - duty) / (1f - duty);
            return P(x0 - reach + 2f * reach * Sm(s), lift * MathF.Sin(MathF.PI * s));
        }
        q.FootFF = Foot(phase, 0.22f); q.FootHN = Foot(phase, -0.16f);
        q.FootFN = Foot(phase + 0.5f, 0.27f); q.FootHF = Foot(phase + 0.5f, -0.21f);
        float bob = 0.012f * MathF.Sin(MathF.Tau * 2f * phase + 0.6f);
        q.Body = P(0f, Lerp(0.34f, 0.245f, low) + bob);
        q.NeckAng = Lerp(36f, 6f, low) + 3f * MathF.Sin(MathF.Tau * phase); q.HeadAng = Lerp(-4f, -28f, low) + 2.5f * MathF.Sin(MathF.Tau * phase + 0.7f);
        q.Pitch = -1.5f * MathF.Sin(MathF.Tau * 2f * phase + 0.6f) - 3f * low;
        q.TailAng = Lerp(-4f, -9f, low) + 3f * MathF.Sin(MathF.Tau * phase + 1f); q.TailCurl = Lerp(10f, 2f, low);
        q.EarF = Lerp(6f, 16f, low); q.EarN = q.EarF;
        return q;
    }

    /// <summary>
    /// The GALLOP (a flat-out dash), phase 0..1 over one stride. The hind feet land nearly together, the fore feet
    /// follow one after the other, and between them the back gathers (short, arched) and stretches (long, flat) with the
    /// body heaving up and down. The brush streams level; the head is low and forward.
    /// </summary>
    public static FPose FGallop(float phase, float stride)
    {
        phase = WrapP(phase);
        var q = new FPose();
        const float duty = 0.30f;
        float reach = stride * duty / 2f;
        PointF Foot(float u, float x0, float lift)
        {
            u = WrapP(u);
            if (u < duty) return P(x0 + reach - 2f * reach * u / duty, 0f);
            float s = (u - duty) / (1f - duty);
            return P(x0 - reach + 2f * reach * Sm(s), lift * MathF.Sin(MathF.PI * s));
        }
        q.FootHN = Foot(phase, -0.12f, 0.20f); q.FootHF = Foot(phase + 0.07f, -0.17f, 0.20f);
        q.FootFN = Foot(phase + 0.42f, 0.24f, 0.17f); q.FootFF = Foot(phase + 0.50f, 0.19f, 0.17f);
        float w = MathF.Sin(MathF.Tau * (phase + 0.18f));
        q.Body = P(0f, 0.35f + 0.045f * (0.5f + 0.5f * MathF.Sin(MathF.Tau * (phase - 0.05f))));
        q.Rx = 0.29f + 0.025f * w; q.Ry = 0.113f - 0.008f * w;
        q.Pitch = 9f * MathF.Sin(MathF.Tau * (phase + 0.12f));
        q.NeckAng = 24f + 6f * w; q.HeadAng = -10f + 5f * w; q.NeckLen = 0.20f;
        q.TailAng = 5f + 3f * MathF.Sin(MathF.Tau * phase + 1f); q.TailCurl = 3f;
        q.EarF = -14f; q.EarN = -14f; q.Mouth = 0.25f;
        return q;
    }

    /// <summary>The phase of the gallop where the four feet are nearest the body: the page the dash can brake from.</summary>
    public static float GallopGather(float stride)
    {
        float best = 0f, score = float.MaxValue;
        for (int i = 0; i < 64; i++)
        {
            var q = FGallop(i / 64f, stride);
            float sc = MathF.Abs(q.FootHN.X + 0.14f) + MathF.Abs(q.FootFN.X - 0.25f) + MathF.Abs(q.FootHF.X + 0.19f) + MathF.Abs(q.FootFF.X - 0.20f) + (q.FootHN.Y + q.FootHF.Y + q.FootFN.Y + q.FootFF.Y) * 2f;
            if (sc < score) { score = sc; best = i / 64f; }
        }
        return best;
    }

    /// <summary>SIT, t 0..1 an idle cycle: the haunches on the snow, the forelegs straight, the brush wrapped round the feet, the chest breathing.</summary>
    public static FPose FSit(float t)
    {
        var q = new FPose
        {
            Body = P(-0.12f, 0.205f), Pitch = 42f, Ry = 0.12f, NeckLen = 0.17f, NeckAng = 74f, HeadAng = 8f,
            FootFF = P(0.11f, 0f), FootFN = P(0.17f, 0f), FootHF = P(0.05f, 0f), FootHN = P(0.11f, 0f), HindFlat = 1f,
            TailWrap = 1f, WrapKind = 0,
        };
        float br = MathF.Sin(MathF.Tau * t * 2f);
        q.Ry += 0.004f * br; q.NeckAng += 1.5f * MathF.Sin(MathF.Tau * t); q.HeadAng += 3f * MathF.Sin(MathF.Tau * t + 1.1f);
        float fl = t > 0.55f && t < 0.68f ? MathF.Sin((t - 0.55f) / 0.13f * MathF.PI) : 0f;
        q.EarN += 18f * fl;
        q.Blink = t > 0.20f && t < 0.28f ? MathF.Sin((t - 0.20f) / 0.08f * MathF.PI) : 0f;
        return q;
    }

    /// <summary>Lying down with the head up (the middle of settling to sleep).</summary>
    public static FPose FLie() => new()
    {
        Body = P(-0.02f, 0.140f), Pitch = 3f, Ry = 0.13f, NeckLen = 0.17f, NeckAng = 56f, HeadAng = 0f, HindFlat = 1f,
        FootFF = P(0.36f, 0f), FootFN = P(0.42f, 0f), FootHF = P(0.08f, 0f), FootHN = P(0.14f, 0f),
        TailWrap = 1f, WrapKind = 0, Blink = 0.3f,
    };

    /// <summary>Curled up asleep: a low dome of a body, the head turned back along the flank, the brush laid over the nose. breathe -1..1, snow 0..1 settled on the back.</summary>
    public static FPose FCurl(float breathe, float snow) => new()
    {
        Body = P(0f, 0.125f + 0.005f * breathe), Pitch = 0f, Rx = 0.30f, Ry = 0.125f + 0.007f * breathe,
        NeckLen = 0.16f, NeckAng = 205f, HeadAng = 182f, HeadFlip = true, EarF = 18f, EarN = 20f, Blink = 1f,
        FootFF = P(0.22f, 0.0f), FootFN = P(0.27f, 0f), FootHF = P(-0.1f, 0f), FootHN = P(-0.04f, 0f), Tuck = 1f, HindFlat = 1f,
        TailWrap = 1f, WrapKind = 1, TailLen = 0.44f, BackSnow = snow * 0.8f,
    };

    // ---- mousing ----

    /// <summary>LISTENING: the fox stops, head low and cocked toward a sound under the snow, the ears swivelling one then the other. t 0..1 (2.4 s).</summary>
    public static FPose FListen(float t)
    {
        var q = FStand(0.2f);
        q.NeckAng = 12f + 3f * MathF.Sin(MathF.Tau * t); q.HeadAng = -50f + 9f * MathF.Sin(MathF.Tau * t * 0.5f + 0.5f);
        q.EarF = 22f + 24f * MathF.Sin(MathF.Tau * t * 2f); q.EarN = 28f + 24f * MathF.Sin(MathF.Tau * t * 2f + 3.14f);
        q.Pitch = -5f; q.Body = P(0f, 0.285f); q.Blink = 0f;
        q.TailAng = -2f + 3f * MathF.Sin(MathF.Tau * t * 3f); q.TailCurl = 4f;
        q.FootFF = P(0.24f, 0f); q.FootFN = P(0.30f, 0f);
        return q;
    }

    /// <summary>The coil before the spring: the hindquarters sunk, the weight on the haunches, the nose still on the spot.</summary>
    public static FPose FCoil()
    {
        var q = FListen(0.1f);
        q.Body = P(-0.01f, 0.235f); q.Pitch = 12f; q.NeckAng = 14f; q.HeadAng = -46f; q.EarF = 30f; q.EarN = 30f;
        q.FootHF = P(-0.12f, 0f); q.FootHN = P(-0.05f, 0f); q.HindFlat = 0.35f; q.TailAng = -6f; q.TailCurl = 8f;
        return q;
    }

    /// <summary>
    /// THE LEAP, t 0..1: the push (hind legs drive), up in an arc (the body highest about 0.45), then over and DOWN, the nose
    /// pointing at the spot, the forelegs reaching, the brush streaming up behind. The last page is the plunge's first.
    /// </summary>
    public static FPose FLeap(float t)
    {
        float[] ts = [0f, 0.2f, 0.45f, 0.75f, 1f];
        var q = new FPose();
        float by = Spline(t, ts, [0.235f, 0.50f, 0.92f, 0.80f, 0.55f]);
        q.Body = P(0f, by);
        q.Pitch = Spline(t, ts, [12f, 38f, 8f, -40f, -62f]);
        q.NeckAng = Spline(t, ts, [14f, 44f, 30f, -30f, -75f]); q.HeadAng = Spline(t, ts, [-46f, 20f, 8f, -50f, -80f]);
        q.EarF = 6f; q.EarN = 6f; q.Mouth = 0f;
        float[] hx = [-0.20f, -0.50f, -0.40f, -0.32f, -0.26f], hy = [-0.235f, -0.28f, -0.36f, -0.30f, -0.14f];
        float[] fx = [0.22f, 0.30f, 0.36f, 0.40f, 0.27f], fy = [-0.235f, -0.12f, -0.22f, -0.44f, -0.55f];
        q.FootHN = P(Spline(t, ts, hx), MathF.Max(0f, by + Spline(t, ts, hy))); q.FootHF = P(q.FootHN.X + 0.07f, q.FootHN.Y + 0.03f);
        q.FootFN = P(Spline(t, ts, fx), MathF.Max(0f, by + Spline(t, ts, fy))); q.FootFF = P(q.FootFN.X - 0.06f, q.FootFN.Y + 0.03f);
        q.TailAng = Spline(t, ts, [-6f, 4f, 10f, 34f, 52f]); q.TailCurl = 6f;
        q.HindFlat = t < 0.15f ? 0.3f : 0f;
        q.Shadow = 1f - 0.55f * Math.Clamp(by / 0.9f, 0f, 1f);
        return q;
    }

    /// <summary>The fox buried to the shoulders: head and forelegs under the snow, the hind legs kicking in the air, the brush up and waving. t cycles; sink 0..1 how far in.</summary>
    public static FPose FPlunge(float t, float sink)
    {
        var q = new FPose
        {
            Body = P(0f, 0.34f - 0.02f * sink), Pitch = -76f, NeckAng = -82f, HeadAng = -86f, NeckLen = 0.19f,
            FootFN = P(0.10f, -0.2f), FootFF = P(0.06f, -0.2f), Clip = true, Lip = 1f,
        };
        float kick = MathF.Sin(MathF.Tau * t * 2f);
        q.FootHN = P(-0.10f + 0.07f * kick, 0.62f + 0.07f * MathF.Sin(MathF.Tau * t * 2f + 1f));
        q.FootHF = P(-0.18f - 0.06f * kick, 0.60f - 0.06f * kick);
        q.TailAng = 82f + 24f * MathF.Sin(MathF.Tau * t * 2f + 2f); q.TailCurl = 12f + 10f * MathF.Sin(MathF.Tau * t * 2f);
        q.EarF = 0f; q.EarN = 0f; q.Shadow = 0.8f;
        return q;
    }

    /// <summary>The last of the dive: from the leap's end (nose at the snow) into the plunge. s 0..1.</summary>
    public static FPose FDiveIn(float s)
    {
        var q = FLerp(FLeap(1f), FPlunge(0f, 0f), Sm(s));
        q.Clip = true; q.Lip = Sm(s);
        return q;
    }

    /// <summary>Backing out with a face full of snow: the fox hauls itself out of the hole, the head rising into the light, snow on the muzzle. s 0..1.</summary>
    public static FPose FBackOut(float s)
    {
        var stand = FStand(0.1f);
        stand.Snow = 1f; stand.Blink = 0.7f; stand.HeadAng = -22f; stand.NeckAng = 30f; stand.Mouth = 0.1f;
        var q = FLerp(FPlunge(0f, 0f), stand, Sm(s));
        q.Clip = s < 0.55f; q.Lip = 1f - Sm((s - 0.3f) / 0.7f);
        q.Snow = 1f;
        return q;
    }

    /// <summary>Shaking the head clear: the head whips from side to side and the ears flap; the snow thins as it goes. t cycles (0.5 s), snow 0..1.</summary>
    public static FPose FShake(float t, float snow)
    {
        var q = FStand(0.1f);
        float w = MathF.Sin(MathF.Tau * t);
        q.HeadAng = -18f + 26f * w; q.NeckAng = 32f + 8f * MathF.Sin(MathF.Tau * t * 2f); q.EarF = 6f + 30f * w; q.EarN = 6f - 30f * w;
        q.Snow = snow; q.Blink = 0.8f; q.Mouth = 0.1f;
        q.Pitch = 2f * w;
        return q;
    }

    /// <summary>A sneeze: the head rears back, then snaps down with the eyes shut and the mouth open. t 0..1.</summary>
    public static FPose FSneeze(float t)
    {
        var q = FStand(0.1f);
        float rear = t < 0.45f ? Sm(t / 0.45f) : 0f;
        float snap = t >= 0.45f && t < 0.62f ? Sm((t - 0.45f) / 0.17f) : t >= 0.62f ? 1f - Sm((t - 0.62f) / 0.38f) : 0f;
        q.NeckAng = 40f + 22f * rear - 52f * snap; q.HeadAng = -6f + 30f * rear - 62f * snap;
        q.Blink = rear * 0.8f + snap; q.Mouth = snap * 0.9f; q.EarF = 6f - 14f * snap; q.EarN = 6f - 14f * snap;
        q.Body = P(0f, 0.31f - 0.02f * snap);
        return q;
    }

    /// <summary>DIGGING at a burrow: the head down at the hole, the near foreleg scraping back along the snow, then lifting forward. t cycles (0.7 s).</summary>
    public static FPose FDig(float t)
    {
        var q = FStand(0.1f);
        q.NeckAng = -8f; q.HeadAng = -62f; q.Pitch = -6f + 3f * MathF.Sin(MathF.Tau * t); q.Body = P(0f, 0.30f);
        q.FootFF = P(0.24f, 0f);
        if (t < 0.5f) q.FootFN = P(Lerp(0.44f, 0.20f, t / 0.5f), 0f);                                   // scrape: back along the snow
        else q.FootFN = P(Lerp(0.20f, 0.44f, Sm((t - 0.5f) / 0.5f)), 0.14f * MathF.Sin(MathF.PI * (t - 0.5f) / 0.5f));   // recover: lifted, forward
        q.EarF = 24f; q.EarN = 24f; q.TailAng = -6f + 4f * MathF.Sin(MathF.Tau * t); q.TailCurl = 4f;
        return q;
    }

    /// <summary>Nosing at the hole: stand with the muzzle down at the snow, the nose working. t cycles.</summary>
    public static FPose FNose(float t)
    {
        var q = FStand(0.1f);
        float b = MathF.Sin(MathF.Tau * t * 2f);
        q.NeckAng = -20f + 3f * b; q.HeadAng = -64f + 5f * b; q.Pitch = -5f; q.Body = P(0f, 0.29f);
        q.EarF = 22f; q.EarN = 22f; q.TailAng = -6f; q.TailCurl = 4f;
        return q;
    }

    /// <summary>The stand turned "yaw" degrees toward us: the pages that turn the fox round.</summary>
    public static FPose FTurn(float yaw)
    {
        var q = FStand(0.1f);
        q.Yaw = yaw; q.EarN = 8f; q.EarF = 8f;
        return q;
    }

    // ---------------------------------------------------------------- the fox's books

    public const float FoxTrotStride = 0.72f, FoxStalkStride = 0.50f, FoxGallopStride = 2.2f;

    public static BookSpec[] FoxBooks()
    {
        BookSpec Make(string name, int n, float halfW, float above, Func<int, FPose> pose) => new()
        {
            Name = name, N = n, HalfW = halfW, Above = above,
            Paint = (g, i) => PaintFox(g, pose(i)),
            Aux = i => FoxNoseOf(pose(i)),
        };
        float rest0 = 0.275f, rest1 = 0.775f, gg = GallopGather(FoxGallopStride);
        return
        [
            Make("stand", 24, 0.80f, 0.80f, i => FStand(i / 24f)),                                         // 0
            Make("look", 5, 0.80f, 0.90f, i => FLook(i / 4f)),                                             // 1
            Make("trot", 24, 0.95f, 0.80f, i => FTrot(i / 24f, FoxTrotStride)),                            // 2
            Make("settle0", 4, 0.85f, 0.80f, i => FLerp(FTrot(rest0, FoxTrotStride), FStand(0.1f), Sm((i + 1) / 4f))),   // 3  trot rest page A to stand
            Make("settle1", 4, 0.85f, 0.80f, i => FLerp(FTrot(rest1, FoxTrotStride), FStand(0.1f), Sm((i + 1) / 4f))),   // 4  trot rest page B to stand
            Make("stalk", 24, 0.95f, 0.70f, i => FTrot(i / 24f, FoxStalkStride, 1f, 0.68f)),               // 5
            Make("stalkIn", 5, 0.90f, 0.70f, i => FLerp(FStand(0.1f), FTrot(0.30f, FoxStalkStride, 1f, 0.68f), Sm((i + 1) / 5f))),   // 6  stand into the crouch
            Make("gallop", 16, 1.35f, 0.85f, i => FGallop(i / 16f, FoxGallopStride)),                      // 7
            Make("skid", 8, 1.10f, 0.85f, i => FLerp(FGallop(gg, FoxGallopStride), FStand(0.1f), Sm((i + 1) / 8f))),    // 8  gallop gather page, braking to a stand
            Make("sit", 12, 0.80f, 0.85f, i => FSit(i / 12f)),                                             // 9
            Make("sitIn", 8, 0.80f, 0.85f, i => FLerp(FStand(0.1f), FSit(0.1f), Sm((i + 1) / 8f))),        // 10 stand to sit
            Make("curlIn", 12, 0.80f, 0.75f, i => i < 6 ? FLerp(FSit(0.1f), FLie(), Sm((i + 1) / 6f)) : FLerp(FLie(), FCurl(0f, 0f), Sm((i - 5) / 6f))),   // 11
            Make("sleep", 16, 0.80f, 0.60f, i => FCurl(MathF.Sin(MathF.Tau * (i % 4) / 4f), (i / 4) / 3f)), // 12 breath cycle x four depths of snow
            Make("listen", 16, 0.85f, 0.70f, i => FListen(i / 16f)),                                       // 13
            Make("coil", 4, 0.85f, 0.70f, i => FLerp(FListen(0.1f), FCoil(), Sm((i + 1) / 4f))),           // 14
            Make("leap", 14, 1.00f, 1.40f, i => FLeap((i + 0.5f) / 14f)),                                  // 15
            Make("diveIn", 4, 0.90f, 1.30f, i => FDiveIn((i + 1) / 4f)),                                   // 16
            Make("plunge", 8, 0.80f, 1.10f, i => FPlunge(i / 8f, 1f)),                                     // 17
            Make("backout", 8, 0.90f, 0.80f, i => FBackOut((i + 1) / 8f)),                                 // 18
            Make("shake", 8, 0.80f, 0.80f, i => FShake(i / 8f, 0.9f - 0.5f * (i / 7f))),                   // 19
            Make("wipe", 4, 0.80f, 0.80f, i => FLerp(FShake(0.1f, 0.4f), FStand(0.1f), Sm((i + 1) / 4f))), // 20 the last of the snow melts off the muzzle
            Make("sneeze", 8, 0.80f, 0.85f, i => FSneeze(i / 8f)),                                         // 21
            Make("dig", 12, 0.90f, 0.70f, i => FDig(i / 12f)),                                             // 22
            Make("noseIn", 4, 0.85f, 0.70f, i => FLerp(FStand(0.1f), FNose(0f), Sm((i + 1) / 4f))),        // 23
            Make("nose", 8, 0.85f, 0.70f, i => FNose(i / 8f)),                                             // 24
            Make("turn", 4, 0.85f, 0.80f, i => FTurn(22.5f * (i + 1))),                                    // 25
            Make("dashIn", 4, 1.0f, 0.75f, i => FLerp(FTrot(0.30f, FoxStalkStride, 1f, 0.68f), FGallop(0f, FoxGallopStride), Sm((i + 1) / 4f))),   // 26 crouch into the dash
        ];
    }

    // ---------------------------------------------------------------- the pose sheet

    /// <summary>
    /// PECKWORKS_SHEET=fox;path.png or hares;path.png: every page of every flip-book, labelled, on the snow's own colour
    /// (222, 233, 252), big (one unit = 130 px), so the animal can be judged page by page BEFORE it is judged in the scene.
    /// A second file (path with "_night" before the extension) lays the first page of each book on night blue (20, 36, 92).
    /// </summary>
    public static void WriteSheet(string which, string path)
    {
        BookSpec[] books = which.StartsWith("fox", StringComparison.OrdinalIgnoreCase) ? FoxBooks() : HareBooks();
        float k = 130f;
        int perRow = 8, margin = 6;
        int cellW = (int)(2 * books.Max(b => b.HalfW) * k) + margin, cellH = (int)(books.Max(b => b.Above) * k) + 30;
        int rows = books.Sum(b => (b.N + perRow - 1) / perRow);
        using var bmp = new Bitmap(cellW * perRow, cellH * rows + 8, PixelFormat.Format32bppArgb);
        using var night = new Bitmap(cellW * perRow, cellH * ((books.Length + perRow - 1) / perRow), PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        using (var gn = Graphics.FromImage(night))
        {
            g.Clear(Color.FromArgb(222, 233, 252)); gn.Clear(Color.FromArgb(20, 36, 92));
            g.SmoothingMode = SmoothingMode.AntiAlias; gn.SmoothingMode = SmoothingMode.AntiAlias;
            int row = 0, bi = 0;
            using var font = new Font("Consolas", 9f);
            foreach (BookSpec b in books)
            {
                for (int i = 0; i < b.N; i++)
                {
                    int cx = (i % perRow) * cellW + cellW / 2, cy = (row + i / perRow) * cellH + cellH - 12;
                    g.DrawString(i == 0 ? $"{b.Name} ({b.N})" : i.ToString(), font, Brushes.Black, cx - cellW / 2 + 2, cy - cellH + 12);
                    var st = g.Save();
                    g.TranslateTransform(cx, cy); g.ScaleTransform(k, -k);
                    b.Paint(g, i);
                    g.Restore(st);
                    g.DrawLine(Pens.Gray, cx - cellW / 2, cy, cx - cellW / 2 + 6, cy);
                }
                int nx = (bi % perRow) * cellW + cellW / 2, ny = (bi / perRow) * cellH + cellH - 12;
                var sn = gn.Save();
                gn.TranslateTransform(nx, ny); gn.ScaleTransform(k, -k);
                b.Paint(gn, 0);
                gn.Restore(sn);
                gn.DrawString(b.Name, font, Brushes.White, nx - cellW / 2 + 2, ny - cellH + 12);
                row += (b.N + perRow - 1) / perRow; bi++;
            }
        }
        bmp.Save(path, ImageFormat.Png);
        night.Save(Path.ChangeExtension(path, null) + "_night.png", ImageFormat.Png);
    }
}
