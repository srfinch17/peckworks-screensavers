using System.Drawing.Drawing2D;
using Peckworks.Screensavers.Core;

namespace Thanksgiving;

/// <summary>
/// How a small songbird is drawn: a junco (slate back, white belly) or a
/// sparrow (brown, streaky), the birds that work a pumpkin patch in the
/// autumn. Everything is in bird units (its body length), x forward, y UP,
/// the origin at the feet on the ground. A bird on the ground hops, pecks
/// and looks; in the air its wings beat (six pages) and its legs tuck.
/// </summary>
internal static class BirdArt
{
    public enum Pose { Stand, Look, Peck, HopCrouch, HopUp, HopLand, Fly }

    private static PointF P(float x, float y) => new(x, y);

    /// <summary>One page on a sheet "w" by "h" pixels, "b" pixels to the bird unit, the feet's origin at (ox, by). "wing" is the flight page (0 to 5) for Pose.Fly; -1 is the wings shut, the fold of a bounding flight.</summary>
    public static Sprite Page(Pose pose, int kind, float b, int w, int h, int ox, int by, int wing = 0) =>
        Sprite.Paint(w, h, g =>
        {
            g.TranslateTransform(ox, by);
            g.ScaleTransform(b, -b);
            Paint(g, pose, kind, wing);
        });

    public static void Paint(Graphics g, Pose pose, int kind, int wing)
    {
        (Color back, Color belly, Color cap, Color beak) = kind == 0
            ? (Color.FromArgb(70, 66, 74), Color.FromArgb(236, 232, 224), Color.FromArgb(56, 52, 60), Color.FromArgb(230, 200, 150))     // a dark-eyed junco
            : (Color.FromArgb(134, 96, 60), Color.FromArgb(214, 196, 160), Color.FromArgb(104, 68, 40), Color.FromArgb(120, 90, 60));    // a song sparrow
        void Ell(float cx, float cy, float rx, float ry, Color c, float tilt = 0)
        {
            var st = g.Save();
            g.TranslateTransform(cx, cy);
            g.RotateTransform(tilt);
            using var br = new SolidBrush(c);
            g.FillEllipse(br, -rx, -ry, rx * 2, ry * 2);
            g.Restore(st);
        }

        bool flying = pose == Pose.Fly;
        bool folded = flying && wing < 0;                                                        // the bounding flight's fold: wings shut, the bird a dart
        float lift = pose == Pose.HopUp ? 0.10f : pose == Pose.HopCrouch ? -0.08f : pose == Pose.HopLand ? -0.04f : 0f;
        float tilt = pose == Pose.Peck ? -34f : pose == Pose.Look ? 6f : pose == Pose.HopUp ? 10f : folded ? -6f : flying ? 4f : 0f;
        // The body sits a fifth of a length clear of the ground on its legs (it used to sit at 0.34 with a 0.27 half-height,
        // so the belly was 0.07 off the ground and the legs 0.03 long: a bird with no feet at any size this patch draws it).
        var body = P(0f, 0.46f + lift);

        // The legs first (behind the body): a songbird's leg is like the turkey's, a hidden thigh up in the feathers, the
        // HOCK (really the ankle) bent BACKWARD just under the belly, then a thin bare shank down to long toes. Two bones
        // reach from a hip inside the body to the foot; the law of cosines puts the hock behind the straight line.
        // Tucked up out of sight in flight.
        if (!flying)
        {
            const float thigh = 0.18f, shank = 0.22f;
            using var leg = new Pen(Color.FromArgb(188, 148, 128), 0.05f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            using var toe = new Pen(Color.FromArgb(188, 148, 128), 0.035f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            // Where the feet are for this pose: under the bird standing, forward on a landing, back and dangling mid-hop.
            (float fx, float fy) = pose switch { Pose.HopUp => (-0.06f, 0.10f), Pose.HopLand => (0.12f, 0f), Pose.Peck => (0.10f, 0f), _ => (0.04f, 0f) };
            foreach (float lx in (float[])[0f, 0.10f])
            {
                PointF hip = P(lx - 0.02f, body.Y - 0.08f), foot = P(lx + fx, fy);
                float dx = foot.X - hip.X, dy = foot.Y - hip.Y;
                float d = MathF.Min(MathF.Sqrt(dx * dx + dy * dy), thigh + shank - 0.002f);
                float cosA = Math.Clamp((thigh * thigh + d * d - shank * shank) / (2 * thigh * d), -1f, 1f);
                float theta = MathF.Atan2(dy, dx) - MathF.Acos(cosA);                               // minus: the hock swings back
                PointF hock = P(hip.X + thigh * MathF.Cos(theta), hip.Y + thigh * MathF.Sin(theta));
                g.DrawLine(leg, hip, hock);
                g.DrawLine(leg, hock, foot);
                g.DrawLine(toe, foot, P(foot.X + 0.12f, foot.Y + 0.005f));                       // the toes forward
                g.DrawLine(toe, foot, P(foot.X + 0.10f, foot.Y + 0.035f));                       // a second toe, fanned
                g.DrawLine(toe, foot, P(foot.X - 0.06f, foot.Y + 0.004f));                       // and the hind toe
            }
        }
        var st2 = g.Save();
        g.TranslateTransform(body.X, body.Y);
        g.RotateTransform(tilt);
        // The far wing in flight, behind the body.
        float beat = flying && !folded ? MathF.Sin(wing / 6f * MathF.Tau) : 0f;
        void Wing(float sx, float sy, float scale, Color c)
        {
            if (!flying || folded) { Ell(sx - 0.08f, sy + 0.02f, 0.30f * scale, 0.11f * scale, c, -8f); return; }   // folded along the side (standing, or the dart of a bounding flight)
            PointF shoulder = P(sx, sy), tip = P(sx - 0.25f, sy + 0.62f * beat * scale), back = P(sx - 0.55f, sy + 0.30f * beat * scale);
            using var br = new SolidBrush(c);
            using var path = new GraphicsPath();
            path.AddClosedCurve([shoulder, P(sx - 0.10f, sy + 0.40f * beat * scale), tip, back, P(sx - 0.30f, sy - 0.02f)], 0.4f);
            g.FillPath(br, path);
        }
        Wing(0.05f, 0.08f, 0.9f, cap);
        // The tail: a short wedge up and back.
        using (var tb = new SolidBrush(cap))
            g.FillPolygon(tb, [P(-0.30f, 0.06f), P(-0.72f, 0.20f + (flying ? -0.1f : 0f)), P(-0.74f, 0.08f + (flying ? -0.1f : 0f)), P(-0.34f, -0.06f)]);
        // The body: a plump oval, the back dark, the belly pale.
        Ell(0f, 0f, 0.46f, 0.27f, back);
        Ell(0.02f, -0.07f, 0.38f, 0.17f, belly);
        if (kind == 1)
            using (var streak = new Pen(Color.FromArgb(120, 90, 60, 36), 0.025f))
                for (int i = 0; i < 4; i++) g.DrawLine(streak, -0.25f + 0.13f * i, 0.16f, -0.28f + 0.13f * i, 0.04f);   // the sparrow's streaks
        // The head: a round head with a cap, a dark eye, a short beak.
        float hx = 0.40f, hy = pose == Pose.Peck ? 0.08f : 0.22f;
        Ell(hx, hy, 0.20f, 0.19f, back);
        Ell(hx, hy + 0.06f, 0.18f, 0.11f, cap);
        if (kind == 1) Ell(hx + 0.08f, hy - 0.03f, 0.10f, 0.07f, belly);                                   // the sparrow's pale cheek
        using (var eye = new SolidBrush(Color.FromArgb(20, 16, 16))) g.FillEllipse(eye, hx + 0.06f, hy + 0.02f, 0.06f, 0.06f);
        using (var bk = new SolidBrush(beak))
            g.FillPolygon(bk, [P(hx + 0.17f, hy + 0.04f), P(hx + 0.34f, hy - 0.02f), P(hx + 0.17f, hy - 0.06f)]);
        // The near wing, over the body.
        Wing(0.08f, 0.10f, 1f, Color.FromArgb(Math.Min(255, cap.R + 30), Math.Min(255, cap.G + 28), Math.Min(255, cap.B + 28)));
        g.Restore(st2);
    }
}
