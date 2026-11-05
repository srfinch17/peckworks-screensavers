using System.Drawing.Drawing2D;
using Peckworks.Screensavers.Core;

namespace Thanksgiving;

/// <summary>
/// How a cottontail rabbit is drawn, and the poses it can take: sitting hunched, grazing with its head down, a hop
/// (and the same hop stretched into a flat-out flight from a coyote), lying dead on its side, and hanging limp from
/// a coyote's mouth. Everything is in rabbit units (about a body length), x forward, y UP, the origin at the feet on
/// the ground. The rabbits live on the far lane behind the deer, so they are small on the screen; the shapes are
/// big and simple (a ball of a body, two long ears, a white puff of a tail) so they still read at thirty pixels.
/// </summary>
internal static class RabbitArt
{
    public static readonly Color Coat = Color.FromArgb(150, 128, 100);
    public static readonly Color CoatDark = Color.FromArgb(112, 92, 68);
    public static readonly Color Belly = Color.FromArgb(222, 210, 190);
    public static readonly Color EarIn = Color.FromArgb(200, 150, 140);
    public static readonly Color Ink = Color.FromArgb(36, 28, 26);

    /// <summary>One pose, in rabbit units.</summary>
    public sealed class Pose
    {
        public PointF Body = new(0f, 0.30f);        // the body's middle
        public float BodyRx = 0.36f, BodyRy = 0.26f, BodyTilt;
        public PointF Head = new(0.36f, 0.46f);
        public float HeadR = 0.17f;
        public float EarAngle = 75f;                // degrees from forward: 75 up and a little back, 160 flat back
        public float EarSpread = 12f;               // the two ears apart
        public PointF HindFoot = new(-0.10f, 0f), ForeFoot = new(0.34f, 0f);
        public float HindLift, ForeLift;            // feet off the ground (flight)
        public bool Dead;                           // lying on its side
    }

    private static PointF P(float x, float y) => new(x, y);
    private static float Lerp(float a, float b, float t) => a + (b - a) * t;
    private static float Smooth(float x) { x = Math.Clamp(x, 0f, 1f); return x * x * (3 - 2 * x); }

    public static Pose Sit() => new();

    public static Pose Graze()
    {
        var q = new Pose { Body = P(0f, 0.27f), BodyRx = 0.38f, BodyRy = 0.24f, Head = P(0.40f, 0.26f), EarAngle = 100f };
        return q;
    }

    /// <summary>
    /// A hop, p 0 to 1: the crouch (0 to 0.2, the body gathered, the hind feet under it), the push and flight (0.2 to
    /// 0.6, stretched out, the fore feet reaching), the landing on the fore feet (0.6 to 0.75) and the hind feet
    /// coming through to land ahead of them (0.75 to 1). "flee" stretches the whole thing flat with the ears back.
    /// The caller moves the rabbit along; this is only the body.
    /// </summary>
    public static Pose Hop(float p, bool flee = false)
    {
        p = ((p % 1f) + 1f) % 1f;
        var q = new Pose();
        float air = p >= 0.2f && p < 0.6f ? MathF.Sin((p - 0.2f) / 0.4f * MathF.PI) : 0f;
        float stretch = p < 0.2f ? Lerp(0.9f, 0.85f, p / 0.2f) : p < 0.6f ? Lerp(0.85f, 1.35f, Smooth((p - 0.2f) / 0.25f)) : Lerp(1.35f, 0.9f, Smooth((p - 0.6f) / 0.4f));
        if (flee) { stretch *= 1.15f; }
        q.BodyRx = 0.36f * stretch; q.BodyRy = 0.26f / MathF.Sqrt(stretch);
        q.Body = P(0f, 0.30f + 0.22f * air * (flee ? 0.8f : 1f));
        q.BodyTilt = p < 0.2f ? -8f : p < 0.6f ? 14f * (1 - (p - 0.2f) / 0.4f) : -10f * (1 - (p - 0.6f) / 0.4f);
        q.Head = P(q.BodyRx + 0.02f, q.Body.Y + 0.14f + 0.04f * air);
        q.EarAngle = flee ? 150f : p < 0.2f ? 85f : 110f + 30f * air;
        // The feet: hind under the body in the crouch, trailing in flight, swinging through to land ahead; fore reaching in flight.
        q.HindFoot = p < 0.2f ? P(-0.12f, 0f) : p < 0.6f ? P(-0.45f, 0f) : P(Lerp(-0.45f, 0.10f, Smooth((p - 0.6f) / 0.4f)), 0f);
        q.HindLift = p >= 0.2f && p < 0.75f ? 0.18f * MathF.Max(air, p >= 0.6f ? 0.5f : 0f) : 0f;
        q.ForeFoot = p < 0.2f ? P(0.30f, 0f) : p < 0.6f ? P(0.55f, 0f) : P(Lerp(0.55f, 0.30f, Smooth((p - 0.6f) / 0.4f)), 0f);
        q.ForeLift = p >= 0.2f && p < 0.6f ? 0.25f * air : 0f;
        return q;
    }

    /// <summary>Dead on its side: the body a long low shape, the legs out, an ear flat on the ground.</summary>
    public static Pose Dead() => new() { Dead = true, Body = P(0f, 0.16f), BodyRx = 0.42f, BodyRy = 0.16f, Head = P(0.42f, 0.14f), HeadR = 0.15f, EarAngle = 175f, EarSpread = 25f, HindFoot = P(-0.30f, 0.06f), ForeFoot = P(0.30f, 0.05f) };

    /// <summary>Hanging limp from a mouth: the sheet's origin is the mouth's grip at the scruff; the rabbit dangles below it.</summary>
    public static Pose Limp() => new() { Dead = true, Body = P(0.02f, -0.42f), BodyRx = 0.22f, BodyRy = 0.36f, BodyTilt = 90f, Head = P(0.02f, -0.12f), HeadR = 0.15f, EarAngle = -80f, EarSpread = 30f, HindFoot = P(-0.05f, -0.86f), ForeFoot = P(0.12f, -0.62f) };

    public static Sprite Page(Pose q, float k, int w, int h, int ox, int by) =>
        Sprite.Paint(w, h, g =>
        {
            g.TranslateTransform(ox, by);
            g.ScaleTransform(k, -k);
            Paint(g, q);
        });

    private static void Ell(Graphics g, float cx, float cy, float rx, float ry, Color c, float tilt = 0)
    {
        var st = g.Save();
        g.TranslateTransform(cx, cy);
        g.RotateTransform(tilt);
        using var b = new SolidBrush(c);
        g.FillEllipse(b, -rx, -ry, rx * 2, ry * 2);
        g.Restore(st);
    }

    public static void Paint(Graphics g, Pose q)
    {
        // The ears first, behind the head: two long leaves from the top of the head, pink inside.
        foreach (float spread in (float[])[-q.EarSpread, q.EarSpread])
        {
            float a = (q.EarAngle + spread) * MathF.PI / 180f;
            PointF root = P(q.Head.X - 0.04f, q.Head.Y + q.HeadR * 0.6f);
            PointF mid = P(root.X + 0.19f * MathF.Cos(a), root.Y + 0.19f * MathF.Sin(a));
            Ell(g, mid.X, mid.Y, 0.21f, 0.065f, Coat, q.EarAngle + spread);
            Ell(g, mid.X, mid.Y, 0.15f, 0.035f, EarIn, q.EarAngle + spread);
        }
        // The far legs, the body, the tail, the near legs, the head.
        void Leg(PointF foot, float lift, bool hind, Color c)
        {
            PointF hip = hind ? P(q.Body.X - q.BodyRx * 0.45f, q.Body.Y - q.BodyRy * 0.2f) : P(q.Body.X + q.BodyRx * 0.55f, q.Body.Y - q.BodyRy * 0.3f);
            PointF f = P(foot.X, foot.Y + lift);
            using var pen = new Pen(c, hind ? 0.09f : 0.06f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            if (hind)
            {
                PointF hock = P((hip.X + f.X) / 2 - 0.08f, (hip.Y + f.Y) / 2 + 0.02f);   // the hock back, a long foot forward
                g.DrawLine(pen, hip, hock); g.DrawLine(pen, hock, f);
                g.DrawLine(pen, f, P(f.X + 0.16f, f.Y));
            }
            else g.DrawLine(pen, hip, f);
        }
        if (q.Dead)
        {
            Ell(g, q.Body.X, q.Body.Y, q.BodyRx, q.BodyRy, Coat, q.BodyTilt);
            Ell(g, q.Body.X + 0.02f, q.Body.Y - 0.02f, q.BodyRx * 0.7f, q.BodyRy * 0.6f, Belly, q.BodyTilt);
            using (var pen = new Pen(CoatDark, 0.07f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                g.DrawLine(pen, P(q.Body.X - q.BodyRx * 0.5f, q.Body.Y), q.HindFoot);
                g.DrawLine(pen, P(q.Body.X + q.BodyRx * 0.5f, q.Body.Y), q.ForeFoot);
            }
            Ell(g, q.Body.X - q.BodyRx * 0.95f, q.Body.Y + 0.02f, 0.07f, 0.06f, Color.White);
            Ell(g, q.Head.X, q.Head.Y, q.HeadR, q.HeadR * 0.9f, Coat);
            Ell(g, q.Head.X + 0.06f, q.Head.Y + 0.02f, 0.035f, 0.025f, Ink);                     // the eye, a slit
            return;
        }
        Leg(P(q.HindFoot.X + 0.06f, q.HindFoot.Y), q.HindLift, true, CoatDark);
        Leg(P(q.ForeFoot.X - 0.05f, q.ForeFoot.Y), q.ForeLift, false, CoatDark);
        Ell(g, q.Body.X, q.Body.Y, q.BodyRx, q.BodyRy, Coat, q.BodyTilt);
        Ell(g, q.Body.X + 0.04f, q.Body.Y - q.BodyRy * 0.3f, q.BodyRx * 0.75f, q.BodyRy * 0.55f, Belly, q.BodyTilt);   // the pale belly
        Ell(g, q.Body.X - q.BodyRx * 0.98f, q.Body.Y + q.BodyRy * 0.1f, 0.075f, 0.07f, Color.White);                  // the cotton tail
        Leg(q.HindFoot, q.HindLift, true, Coat);
        Leg(q.ForeFoot, q.ForeLift, false, Coat);
        Ell(g, q.Head.X, q.Head.Y, q.HeadR, q.HeadR * 0.92f, Coat);
        Ell(g, q.Head.X + q.HeadR * 0.55f, q.Head.Y - q.HeadR * 0.25f, q.HeadR * 0.55f, q.HeadR * 0.45f, Belly);      // the cheek
        Ell(g, q.Head.X + q.HeadR * 0.95f, q.Head.Y - 0.01f, 0.03f, 0.025f, Ink);                                     // the nose
        Ell(g, q.Head.X + q.HeadR * 0.35f, q.Head.Y + q.HeadR * 0.2f, 0.045f, 0.047f, Ink);                           // the eye
        Ell(g, q.Head.X + q.HeadR * 0.42f, q.Head.Y + q.HeadR * 0.28f, 0.014f, 0.014f, Color.White);
    }
}
