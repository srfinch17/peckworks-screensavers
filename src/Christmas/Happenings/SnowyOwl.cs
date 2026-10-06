using System.Drawing.Drawing2D;
using Peckworks.Screensavers.Core;

namespace Christmas.Happenings;

/// <summary>
/// A snowy owl fades in on a bare twig in one of the top corners, blinks its
/// big yellow eyes twice, turns its head to look left, then right, then back
/// to the front, ruffles its feathers once, and fades away.
///
/// FEYNMAN VERSION: the owl is a flip-book where each page is the WHOLE owl
/// (body and head painted together, so there is no seam between them). A page
/// is picked by three small numbers: how puffed the feathers are (3 steps),
/// where the head is looking (5 steps from far left to far right, which
/// plays as a smooth turn) and how shut the eyelids are (8 steps; a blink
/// plays through them in a third of a second). Not every combination is
/// needed: while the head is turning the eyes are open, and blinks happen
/// while it looks ahead. That makes 3 x (5 + 8) = 39 small pages.
///
/// An owl is white, and the sky is dark, so it stands out on its own, but a
/// soft pale grey-blue edge (painted first, wide, and then covered by the
/// white) keeps the outline crisp. The eyes sit on a warm yellow patch that
/// fades out into the face, and a small real Glow is stamped over each eye
/// too, so they shine like lit windows.
///
/// Where it sits: in Begin it looks through the painter's list of twig spots
/// for one that is on screen, in the top 40 percent, away from the moon (its
/// glow would wash a white owl out), with room above it for the owl, and with
/// the fewest bulbs where its body will be. If none fits, it ends at once.
/// The owl is in front of the branch, so there is no stencil.
/// </summary>
internal sealed class SnowyOwl : Happening
{
    private const float Total = 12f;
    private const int Puffs = 3, Looks = 5, Blinks = 8;

    private static readonly Color Fur = Color.FromArgb(250, 251, 255);
    private static readonly Color Edge = Color.FromArgb(165, 184, 218);
    private static readonly Color Fleck = Color.FromArgb(150, 160, 182);
    private static readonly Color Eye = Color.FromArgb(255, 204, 36);
    private static readonly Color Dark = Color.FromArgb(24, 20, 30);
    private static readonly Color Toes = Color.FromArgb(226, 220, 206);

    // How closed the lid is on each of the 8 blink pages (0 open, 1 shut).
    private static readonly float[] Lid = [0f, 0.3f, 0.65f, 1f, 1f, 0.65f, 0.3f, 0f];

    private readonly ChristmasScenery _s;
    private readonly float _k;                       // pixels per drawing unit; the owl is about 1.05 units tall = 0.06 U
    private readonly int _w, _h, _ox, _by;
    private readonly Sprite[,] _look = new Sprite[Puffs, Looks];     // [puff, where the head looks], eyes open
    private readonly Sprite[,] _blink = new Sprite[Puffs, Blinks];   // [puff, how shut], looking ahead
    private readonly Sprite _glow;

    private bool _ok;
    private PointF _spot;

    public override float Seconds => _ok ? Total : 0.1f;
    public override int Layer => 1;                        // in front of the scenery, Santa and the lights (see ChristmasScene.Render)

    public SnowyOwl(ChristmasScenery s)
    {
        _s = s;
        _k = Math.Max(16f, s.U * 0.057f);
        _w = (int)(0.9f * _k) + 6;
        _ox = _w / 2;
        _h = (int)(1.12f * _k) + 6;
        _by = _h - 3;
        _glow = Sprite.Glow(Math.Max(3, (int)(0.15f * _k)), Color.FromArgb(255, 214, 90));

        for (int p = 0; p < Puffs; p++)
        {
            for (int l = 0; l < Looks; l++) _look[p, l] = Page(p / (Puffs - 1f), (l - 2) / 2f, 0f);
            for (int b = 0; b < Blinks; b++) _blink[p, b] = Page(p / (Puffs - 1f), 0f, Lid[b]);
        }
    }

    public override void Begin(Random rng)
    {
        _ok = false;
        float u = _s.U;
        float halfW = 0.3f * _k, tall = 1.08f * _k;
        var good = new List<(float Score, PointF At)>();
        foreach (PointF p in _s.CornerBranchSpots)
        {
            if (p.X < halfW || p.X > _s.Width - halfW || p.Y < tall || p.Y > _s.Height * 0.4f) continue;
            float mx = p.X - _s.Moon.At.X, my = p.Y - _s.Moon.At.Y;
            if (MathF.Sqrt(mx * mx + my * my) < 0.15f * u) continue;
            // Count bulbs where the owl's body would hide them or sit on top of them.
            int bulbs = 0;
            foreach (var (at, _) in _s.Lights)
                if (MathF.Abs(at.X - p.X) < 0.38f * _k && at.Y > p.Y - tall && at.Y < p.Y + 0.05f * _k) bulbs++;
            good.Add((bulbs + (float)rng.NextDouble() * 0.5f, p));    // a little noise so ties are broken at random
        }
        if (good.Count == 0) return;
        _spot = good.OrderBy(g => g.Score).First().At;
        _ok = true;
    }

    // ------------------------------------------------------------------ draw

    public override void Draw(FrameBuffer fb, float t)
    {
        if (!_ok) return;
        float op = Fade(t, Total, 1f, 1.3f);

        // Where the head looks, from the timetable: ahead, then left, then right (through the middle), then ahead.
        float look = 0;
        if (t > 4.2f) look = -Smooth((t - 4.2f) / 0.9f);
        if (t > 5.9f) look = -1 + 2 * Smooth((t - 5.9f) / 1.2f);
        if (t > 7.9f) look = 1 - Smooth((t - 7.9f) / 0.9f);
        int li = (int)MathF.Round((look + 1) * (Looks - 1) / 2);

        // Blinks at 1.6 s and 3.0 s: play the 8 lid pages in 0.32 seconds.
        int blink = -1;
        foreach (float at in new[] { 1.6f, 3.0f })
            if (t >= at && t < at + 0.32f) blink = (int)((t - at) / 0.32f * Blinks);
        float lid = blink >= 0 ? Lid[blink] : 0;

        // The ruffle: the feathers puff out and shake back twice, quickly, around 9.0 s.
        int puff = 0;
        if (t > 9f && t < 10f) puff = (int)MathF.Round(Math.Max(0, MathF.Sin((t - 9f) * MathF.PI * 2.2f)) * (Puffs - 1));

        Sprite page = blink >= 0 ? _blink[puff, Math.Min(Blinks - 1, blink)] : _look[puff, li];
        int left = (int)MathF.Round(_spot.X) - _ox;
        int top = (int)MathF.Round(_spot.Y) - _by + 1;       // the toes grip the twig, a pixel into it
        page.Draw(fb, left, top, op);

        // A real glow over each eye, dimmer while the lid is down.
        float g = op * 0.55f * (1 - lid);
        if (g > 0.02f)
            foreach (float side in new[] { -1f, 1f })
            {
                float ex = EyeX((li - 2) / 2f, side), ey = EyeY;   // the same quantised look as the page, so the glow sits on the eyes
                _glow.DrawCentered(fb, left + _ox + ex * _k, top + _by - ey * _k, g);
            }
    }

    private const float EyeY = 0.77f;
    private static float EyeX(float look, float side) => look * 0.15f + side * 0.105f * (1 - 0.12f * MathF.Abs(look));

    // ----------------------------------------------------------------- paint

    private Sprite Page(float puff, float look, float lid) =>
        Sprite.Paint(_w, _h, g =>
        {
            g.TranslateTransform(_ox, _by);
            g.ScaleTransform(_k, -_k);                 // units: x across, y UP from the twig
            float edge = 1.5f / _k;
            for (int pass = 0; pass < 2; pass++) Owl(g, pass, edge, puff, look, lid);
        });

    private static void Owl(Graphics g, int pass, float edge, float puff, float look, float lid)
    {
        using var edgePen = new Pen(Edge, edge * 2) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round };
        using var edgeBrush = new SolidBrush(Edge);

        void Oval(float cx, float cy, float rx, float ry, float tilt, Color c, bool outline = true)
        {
            var st = g.Save();
            g.TranslateTransform(cx, cy);
            g.RotateTransform(tilt);
            using var path = new GraphicsPath();
            path.AddEllipse(-rx, -ry, rx * 2, ry * 2);
            if (pass == 0) { if (outline) { g.FillPath(edgeBrush, path); g.DrawPath(edgePen, path); } }
            else { using var b = new SolidBrush(c); g.FillPath(b, path); }
            g.Restore(st);
        }

        float wide = 1 + 0.2f * puff, high = 1 + 0.08f * puff;      // puffed feathers: wider and a little taller

        // ---- tail, toes, body, wings, head ----
        {
            // The tail: a short wedge hanging down behind the perch.
            using var path = new GraphicsPath();
            path.AddPolygon([new PointF(-0.12f, 0.12f), new PointF(0.12f, 0.12f), new PointF(0.09f, -0.05f), new PointF(-0.09f, -0.05f)]);
            if (pass == 0) { g.FillPath(edgeBrush, path); g.DrawPath(edgePen, path); }
            else { using var b = new SolidBrush(Color.FromArgb(236, 238, 246)); g.FillPath(b, path); }
        }
        foreach (float sx in new[] { -0.09f, 0.09f })
            Oval(sx, 0.045f, 0.075f, 0.05f, 0, Toes);                                        // feathered toes gripping the twig
        Oval(0, 0.38f * high, 0.27f * wide, 0.37f * high, 0, Fur);                           // the body
        Oval(0, 0.77f, 0.25f * (1 + 0.04f * puff), 0.25f, 0, Fur);                           // the head, merged into the shoulders (owls have almost no neck)
        Oval(-0.20f * wide, 0.34f, 0.09f * wide, 0.26f, -8, Color.FromArgb(240, 242, 250));  // folded wings at each side
        Oval(0.20f * wide, 0.34f, 0.09f * wide, 0.26f, 8, Color.FromArgb(240, 242, 250));

        if (pass == 0) return;

        // ---- speckles: tiny grey flecks and short bars on the chest, wings and crown (a fixed pattern) ----
        var rng = new Random(11);
        using (var fleck = new SolidBrush(Color.FromArgb(150, Fleck)))
        {
            for (int i = 0; i < 46; i++)
            {
                float a = (float)(rng.NextDouble() * Math.PI * 2), r = (float)Math.Sqrt(rng.NextDouble());
                bool crown = i % 4 == 0;
                float cx, cy;
                if (crown) { cx = MathF.Cos(a) * r * 0.2f; cy = 0.77f + MathF.Abs(MathF.Sin(a)) * r * 0.2f; if (cy < 0.88f) continue; }   // only the top of the head
                else { cx = MathF.Cos(a) * r * 0.21f * wide; cy = 0.38f + MathF.Sin(a) * r * 0.30f - 0.06f; if (cy > 0.64f) continue; }
                g.FillEllipse(fleck, cx - 0.013f, cy - 0.008f, 0.026f, 0.016f);
            }
            for (int i = 0; i < 7; i++)                                                      // darker bars across the wings
            {
                float y = 0.16f + i * 0.065f;
                g.FillRectangle(fleck, -0.27f * wide, y, 0.1f, 0.012f);
                g.FillRectangle(fleck, 0.17f * wide, y, 0.1f, 0.012f);
            }
        }

        // ---- the face: eyes on warm yellow patches, a small dark beak ----
        float fx = look * 0.15f;
        using (var tone = new SolidBrush(Color.FromArgb(70, 255, 214, 100)))
            g.FillEllipse(tone, fx - 0.22f, 0.77f - 0.12f, 0.44f, 0.24f);                    // a faint warm patch behind both eyes
        foreach (float side in new[] { -1f, 1f })
        {
            float ex = EyeX(look, side), ey = EyeY, r = 0.077f;
            using var halo = new GraphicsPath();
            halo.AddEllipse(ex - r * 1.7f, ey - r * 1.7f, r * 3.4f, r * 3.4f);
            using (var pg = new PathGradientBrush(halo) { CenterColor = Color.FromArgb(120, 255, 214, 80), SurroundColors = [Color.FromArgb(0, 255, 214, 80)] })
                g.FillPath(pg, halo);
            using var disc = new GraphicsPath();
            disc.AddEllipse(ex - r, ey - r, r * 2, r * 2);
            using (var yb = new SolidBrush(Eye)) g.FillPath(yb, disc);
            using (var pb = new SolidBrush(Dark)) g.FillEllipse(pb, ex + look * 0.02f - 0.036f, ey - 0.036f, 0.072f, 0.072f);   // the pupil, looking the same way
            using (var hi = new SolidBrush(Color.FromArgb(230, 255, 255, 255))) g.FillEllipse(hi, ex + look * 0.02f - 0.026f, ey + 0.008f, 0.02f, 0.02f);
            if (lid > 0)
            {
                // The eyelid: a feather-colored cover sliding down from the top, clipped to the eye.
                var st = g.Save();
                g.SetClip(disc, CombineMode.Intersect);
                using var fur = new SolidBrush(Fur);
                g.FillRectangle(fur, ex - r - 0.01f, ey + r - lid * 2 * r, 2 * r + 0.02f, lid * 2 * r + 0.02f);
                g.Restore(st);
                if (lid > 0.9f)
                {
                    using var line = new Pen(Fleck, 0.012f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                    g.DrawLine(line, ex - r * 0.8f, ey + r * 0.05f, ex + r * 0.8f, ey + r * 0.05f);
                }
            }
        }
        using (var beak = new SolidBrush(Color.FromArgb(60, 52, 58)))
            g.FillPolygon(beak, [new PointF(fx - 0.03f, 0.725f), new PointF(fx + 0.03f, 0.725f), new PointF(fx, 0.655f)]);
    }
}
