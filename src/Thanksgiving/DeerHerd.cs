using System.Drawing.Drawing2D;
using Peckworks.Screensavers.Core;

namespace Thanksgiving;

/// <summary>
/// A white-tailed doe, and her fawn, that LIVE at the far side of the field
/// (his line: "add a deer ffs"). The doe grazes with her head down, lifts
/// it to look about, flicks an ear, and walks a few steps; the fawn keeps
/// close to her, grazing beside her and trotting after her when she moves
/// off. Like the turkeys, they are always there.
///
/// FEYNMAN VERSION: each deer is two flip-books stamped together. The BODY
/// book has sixteen pages of a walk (page 0 is also standing): four legs
/// each a quarter of a step behind the last, the foot sliding back along
/// the ground and then swinging forward, with the knee worked out by a
/// little two-bone sum (a deer's hind "knee" is its hock and points
/// BACKWARD, the fore knee points forward). The HEAD book has six pages
/// from alert (head up, ears pricked) to grazing (nose in the stubble),
/// drawn on its own sheet whose middle is the neck's root, so it can be
/// stamped on the body at the neck wherever the body page is. One picture
/// of each, facing right; the engine's mirror stamp turns them to face
/// left, so nothing is painted twice.
///
/// They are stamped through the painter's OpenField stencil, so the
/// scarecrow, the fence, the near ground and the maples hide them where
/// they should. They stand near the field's far lane, so the turkeys (which
/// wander nearer) are drawn after them, and the tractor on the lane (a
/// layer 0 happening) passes behind them.
/// </summary>
internal sealed class DeerHerd
{
    private enum Doing { Graze, Look, Walk }

    private sealed class Animal
    {
        public float H;                                     // body height in pixels (the unit everything is drawn in)
        public int BodyW, BodyH, BodyOx, BodyOy, HeadS;
        public Sprite[] Body = new Sprite[WalkPages + 1];           // the walk, then one standing page (feet square under the hips)
        public Sprite[] Head = new Sprite[HeadPages];
        public bool Fawn;
        public float X, Depth;
        public int Dir;
        public Doing Doing;
        public float Timer, Clock, Walked;
        public float HeadF;                                 // where the head is now, 0 up to 1 grazing (eased toward its target)
    }

    private const int WalkPages = 16, HeadPages = 6;
    private const float Speed = 0.05f;                      // in U per second
    private const float StepReach = 0.17f;                  // how far a foot travels in front of and behind the hip, in body heights
    private const float NeckX = 0.30f, NeckY = 0.72f;       // where the neck leaves the body (body heights from the middle, up from the ground)

    private static readonly Color Tan = Color.FromArgb(178, 128, 84);
    private static readonly Color TanDark = Color.FromArgb(142, 98, 62);
    private static readonly Color Cream = Color.FromArgb(236, 224, 200);
    private static readonly Color Hoof = Color.FromArgb(40, 30, 26);
    private static readonly Color Nose = Color.FromArgb(32, 26, 26);
    private static readonly Color EarPink = Color.FromArgb(214, 170, 150);

    private readonly ThanksgivingScenery _s;
    private readonly Random _rng;
    private readonly Animal[] _deer;

    public DeerHerd(ThanksgivingScenery s, int count, Random sceneRng)
    {
        _s = s;
        _rng = new Random(sceneRng.Next());                 // its own dice, so the rest of the scene never reshuffles
        _deer = new Animal[count];
        float x0 = s.Width * (0.10f + 0.75f * (float)_rng.NextDouble());
        int dir = _rng.Next(2) == 0 ? 1 : -1;
        for (int i = 0; i < count; i++)
        {
            bool fawn = i == 1;                             // the second deer is the fawn; a third is another doe
            var a = new Animal { Fawn = fawn, Dir = dir, Depth = 0.18f + 0.10f * (float)_rng.NextDouble() + (fawn ? 0.06f : 0f) };
            a.X = x0 - dir * (i == 0 ? 0 : (fawn ? s.U * 0.09f : s.U * 0.22f));
            a.Doing = Doing.Graze; a.HeadF = 1f; a.Timer = 2f + 4f * (float)_rng.NextDouble();
            Make(a, Math.Max(14, (int)(s.U * (fawn ? 0.048f : 0.078f))));
            _deer[i] = a;
        }
    }

    // ================================================================ painting (once)

    private static void Make(Animal a, int h)
    {
        a.H = h;
        a.BodyW = (int)(1.3f * h) + 4;
        a.BodyOx = a.BodyW / 2;
        a.BodyH = (int)(0.95f * h) + 4;
        a.BodyOy = a.BodyH - 2;
        a.HeadS = (int)(1.05f * h) + 4;
        for (int i = 0; i <= WalkPages; i++)
        {
            int pose = i;                                                // WalkPages itself is the standing page
            a.Body[i] = Sprite.Paint(a.BodyW, a.BodyH, g =>
            {
                g.TranslateTransform(a.BodyOx, a.BodyOy);
                g.ScaleTransform(h, -h);                        // body heights, y UP
                PaintBody(g, pose, a.Fawn, h);
            });
        }
        for (int j = 0; j < HeadPages; j++)
        {
            float f = j / (float)(HeadPages - 1);
            a.Head[j] = Sprite.Paint(a.HeadS, a.HeadS, g =>
            {
                g.TranslateTransform(a.HeadS / 2f, a.HeadS * 0.55f);
                g.ScaleTransform(h, -h);
                PaintHead(g, f, a.Fawn, h);
            });
        }
    }

    private static PointF P(float x, float y) => new(x, y);

    /// <summary>The body and legs: x forward, y up, the origin at the middle of the body on the ground.</summary>
    private static void PaintBody(Graphics g, int pose, bool fawn, int h)
    {
        float pw = MathF.Max(0.01f, 1.0f / h);                 // one pixel, in body heights
        void Blob(PointF[] pts, Color c, float tension = 0.5f)
        {
            using var path = new GraphicsPath();
            path.AddClosedCurve(pts, tension);
            using var b = new SolidBrush(c);
            g.FillPath(b, path);
        }
        void Limb(PointF a, PointF b, float w, Color c)
        {
            using var pen = new Pen(c, w) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawLine(pen, a, b);
        }

        // Legs: a lateral walk, each leg a quarter of a cycle after the last. Far legs first (darker), the body, then the near legs.
        void Leg(float hipX, float offset, bool hind, Color upper)
        {
            float ph = (pose / (float)WalkPages + offset) % 1f;
            float dx, up = 0;
            if (pose == WalkPages) dx = hipX < 0 ? -0.03f : 0.03f;        // standing: each foot square under its hip, the pairs a touch apart
            else if (ph < 0.7f) dx = StepReach * (1 - 2 * ph / 0.7f);     // standing on it: the foot slides back along the ground
            else
            {
                float q = (ph - 0.7f) / 0.3f;
                dx = StepReach * (-1 + 2 * q);                            // in the air: it swings forward again
                up = 0.07f * MathF.Sin(MathF.PI * q);
            }
            PointF hip = P(hipX, 0.52f), foot = P(hipX + dx, 0.02f + up);
            // Two-bone IK: the joint goes where two bones of equal length reach from hip to foot; hocks point back, knees forward.
            const float bone = 0.265f;                                  // two bones just longer than hip to ground: a standing leg is nearly straight, with only a slight bend at the joint (0.30 made a zigzag that read as an X)
            float ddx = foot.X - hip.X, ddy = foot.Y - hip.Y;
            float d = MathF.Min(MathF.Sqrt(ddx * ddx + ddy * ddy), 2 * bone - 0.002f);
            float alpha = MathF.Acos(Math.Clamp(d / (2 * bone), -1, 1));
            float theta = MathF.Atan2(ddy, ddx) + (hind ? alpha : -alpha);
            PointF joint = P(hip.X + bone * MathF.Cos(theta), hip.Y + bone * MathF.Sin(theta));
            Limb(hip, joint, MathF.Max(0.075f, 2.2f * pw), upper);
            Limb(joint, foot, MathF.Max(0.05f, 1.6f * pw), TanDark);
            Limb(P(foot.X - 0.015f, foot.Y), P(foot.X + 0.025f, foot.Y), MathF.Max(0.04f, 1.5f * pw), Hoof);
        }
        Leg(-0.26f, 0.50f, true, TanDark);
        Leg(0.26f, 0.75f, false, TanDark);

        // The torso: deep chest, a straight back, a rounded rump; the belly and throat cream.
        Blob([P(0.42f, 0.60f), P(0.32f, 0.78f), P(0.0f, 0.82f), P(-0.34f, 0.78f), P(-0.47f, 0.62f), P(-0.36f, 0.46f), P(0.0f, 0.42f), P(0.34f, 0.46f)], Tan);
        Blob([P(0.30f, 0.50f), P(0.0f, 0.44f), P(-0.32f, 0.48f), P(-0.30f, 0.56f), P(0.0f, 0.54f), P(0.30f, 0.56f)], Cream, 0.4f);
        // The tail: a short tuft at the rump, white underneath (the white tail that names the deer).
        Blob([P(-0.44f, 0.76f), P(-0.56f, 0.72f), P(-0.57f, 0.62f), P(-0.47f, 0.64f)], Tan, 0.4f);
        Blob([P(-0.50f, 0.68f), P(-0.57f, 0.63f), P(-0.53f, 0.60f), P(-0.46f, 0.64f)], Cream, 0.4f);
        if (fawn)
        {
            // A fawn's spots: two rows of cream dots along the back.
            using var spot = new SolidBrush(Cream);
            for (int i = 0; i < 6; i++)
            {
                float sx = -0.30f + 0.11f * i, r = 0.028f;
                g.FillEllipse(spot, sx - r, 0.72f - r, r * 2, r * 2);
                g.FillEllipse(spot, sx + 0.05f - r, 0.63f - r, r * 2, r * 2);
            }
        }

        Leg(-0.22f, 0.0f, true, Tan);
        Leg(0.22f, 0.25f, false, Tan);
    }

    /// <summary>The neck and head on a sheet whose origin is the neck's root. f: 0 = head up and alert, 1 = nose down in the stubble.</summary>
    private static void PaintHead(Graphics g, float f, bool fawn, int h)
    {
        float pw = MathF.Max(0.01f, 1.0f / h);
        float a = (24f + 136f * f) * MathF.PI / 180f;                      // the neck's angle from straight up, forward
        float ha = (84f + 90f * f) * MathF.PI / 180f;                      // the head's axis: forward when alert, down when grazing
        float len = 0.30f + 0.10f * f;                                     // the neck stretches a little to reach the ground
        PointF root = P(0, 0);
        PointF top = P(len * MathF.Sin(a), len * MathF.Cos(a));
        PointF Along(PointF from, float ang, float d) => P(from.X + d * MathF.Sin(ang), from.Y + d * MathF.Cos(ang));
        PointF headMid = Along(top, ha, 0.10f);

        using (var neck = new Pen(Tan, MathF.Max(0.13f, 3.5f * pw)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            g.DrawLine(neck, root, top);
        using (var throat = new Pen(Cream, MathF.Max(0.05f, 1.5f * pw)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            g.DrawLine(throat, Along(root, a, 0.06f), Along(Along(root, a, 0.06f), a, len * 0.6f));   // the pale stripe down the front of the neck

        // Two big ears, back from the head, standing up whatever the head is doing; the far one first.
        foreach (float spread in new[] { -0.30f, 0.34f })
        {
            float ea = ha - 1.95f + spread;
            PointF mid = Along(top, ea, 0.10f);
            var es = g.Save();
            g.TranslateTransform(mid.X, mid.Y);
            g.RotateTransform(90f - ea * 180f / MathF.PI);
            using (var ear = new SolidBrush(Tan)) g.FillEllipse(ear, -0.10f, -0.045f, 0.20f, 0.09f);
            using (var inner = new SolidBrush(EarPink)) g.FillEllipse(inner, -0.06f, -0.025f, 0.13f, 0.05f);
            g.Restore(es);
        }

        // The head: an oval along its axis with a slimmer muzzle, a cream muzzle ring, a black nose, an eye with a glint.
        var st = g.Save();
        g.TranslateTransform(headMid.X, headMid.Y);
        g.RotateTransform(90f - ha * 180f / MathF.PI);                     // in this turned frame +x is the way the nose points
        float ml = fawn ? 0.16f : 0.21f;                                   // a fawn's face is shorter
        using (var skull = new SolidBrush(Tan)) g.FillEllipse(skull, -0.12f, -0.075f, 0.24f, 0.15f);
        using (var muzzle = new SolidBrush(Tan))
            g.FillPolygon(muzzle, [P(0.04f, 0.04f), P(ml, 0.035f), P(ml + 0.03f, -0.005f), P(ml, -0.045f), P(0.04f, -0.07f)]);
        using (var ring = new SolidBrush(Cream))
            g.FillPolygon(ring, [P(ml - 0.06f, 0.03f), P(ml, 0.03f), P(ml + 0.03f, -0.005f), P(ml, -0.045f), P(ml - 0.06f, -0.06f)]);
        using (var nose = new SolidBrush(Nose)) g.FillEllipse(nose, ml - 0.005f, -0.03f, 0.045f, 0.04f);
        using (var eye = new SolidBrush(Nose)) g.FillEllipse(eye, 0.0f, 0.0f, MathF.Max(0.035f, 2f * pw), MathF.Max(0.035f, 2f * pw));
        using (var glint = new SolidBrush(Color.White)) g.FillEllipse(glint, 0.018f, 0.018f, MathF.Max(0.012f, pw), MathF.Max(0.012f, pw));
        g.Restore(st);
    }

    // ================================================================ living

    private float FootY(Animal a) => _s.FieldY(a.X) + (_s.Ground.YAt(Math.Clamp(a.X, 0, _s.Width - 1)) - _s.U * 0.006f - _s.FieldY(a.X)) * a.Depth;

    public void Update(float dt)
    {
        float u = _s.U;
        Animal doe = _deer.Length > 0 ? _deer[0] : null!;
        for (int i = 0; i < _deer.Length; i++)
        {
            Animal a = _deer[i];
            a.Timer -= dt;
            a.Clock += dt;
            // The head eases toward where the doing wants it: down for grazing (with small nibbles), up to look, half way to walk.
            float want = a.Doing switch
            {
                Doing.Graze => 0.92f + 0.08f * MathF.Sin(a.Clock * 5f),
                Doing.Look => 0f,
                _ => 0.25f,
            };
            a.HeadF += (want - a.HeadF) * MathF.Min(1f, dt * 4f);

            if (a.Doing == Doing.Walk)
            {
                float step = Speed * u * dt;
                a.X += a.Dir * step;
                a.Walked += step;
                float margin = a.H * 0.9f;
                if (a.X < margin) { a.X = margin; a.Dir = 1; }
                if (a.X > _s.Width - margin) { a.X = _s.Width - margin; a.Dir = -1; }
            }

            if (a.Fawn && doe != null)
            {
                // The fawn keeps close: if the doe has moved off, trot after her; otherwise do as she does, a little behind.
                float want_x = doe.X - doe.Dir * u * 0.09f;
                float gap = want_x - a.X;
                if (MathF.Abs(gap) > u * 0.05f)
                {
                    if (a.Doing != Doing.Walk) { a.Doing = Doing.Walk; a.Clock = 0; }
                    a.Dir = gap > 0 ? 1 : -1;
                    a.Timer = 0.3f;
                    continue;
                }
                if (a.Doing == Doing.Walk && MathF.Abs(gap) <= u * 0.02f) { a.Doing = Doing.Graze; a.Timer = 2f + 3f * (float)_rng.NextDouble(); a.Clock = 0; a.Dir = doe.Dir; }
            }
            if (a.Timer > 0) continue;

            a.Clock = 0;
            double roll = _rng.NextDouble();
            if (a.Doing == Doing.Walk || roll < 0.55) { a.Doing = Doing.Graze; a.Timer = 3f + 6f * (float)_rng.NextDouble(); }
            else if (roll < 0.80) { a.Doing = Doing.Look; a.Timer = 1.5f + 2.5f * (float)_rng.NextDouble(); }
            else if (!a.Fawn) { a.Doing = Doing.Walk; a.Timer = 2f + 5f * (float)_rng.NextDouble(); if (_rng.Next(3) == 0) a.Dir = -a.Dir; }
        }
    }

    /// <summary>Stamps the herd through the field stencil: the doe, then the fawn (nearer), then any other doe.</summary>
    public void Draw(FrameBuffer fb)
    {
        foreach (Animal a in _deer)
        {
            bool mirror = a.Dir < 0;
            float foot = FootY(a);
            int page;
            if (a.Doing == Doing.Walk)
            {
                float stride = 4 * StepReach * a.H;
                float cycle = a.Walked / stride;
                page = (int)MathF.Round((cycle - MathF.Floor(cycle)) * WalkPages) % WalkPages;
            }
            else page = WalkPages;                                        // the standing page
            int left = (int)MathF.Round(a.X) - a.BodyOx;
            int top = (int)MathF.Round(foot) - a.BodyOy;
            a.Body[page].Draw(fb, left, top, 1f, _s.OpenField, mirror);
            // The head sheet's origin is the neck's root: on the body at (NeckX, NeckY) body heights, mirrored with the body.
            int hp = Math.Clamp((int)MathF.Round(a.HeadF * (HeadPages - 1)), 0, HeadPages - 1);
            float neckX = a.X + (mirror ? -1 : 1) * NeckX * a.H, neckY = foot - NeckY * a.H;
            int hl = (int)MathF.Round(neckX) - a.HeadS / 2, ht = (int)MathF.Round(neckY) - (int)(a.HeadS * 0.55f);
            a.Head[hp].Draw(fb, hl, ht, 1f, _s.OpenField, mirror);
        }
    }
}
