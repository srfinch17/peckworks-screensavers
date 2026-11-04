using System.Drawing.Drawing2D;
using Peckworks.Screensavers.Core;

namespace Thanksgiving.Happenings;

/// <summary>
/// A bright red cardinal swoops in from off the top of the screen, flares its
/// wings and lands on a twig of one of the corner branches, folds up, looks
/// about (it turns its head right round to look behind it), lifts its head and
/// sings three phrases with its beak opening and closing, flicks its tail, and
/// crouches and flies off up and away.
///
/// FEYNMAN VERSION: a bird is three flip-books glued together in time.
///  - FLYING: 16 pages of the wings going round once (the wing is a blade that
///    swings up and back, then down and back, and bends at the tip), painted
///    twice more with the body tilted nose-up (for the take-off climb) and
///    with the feet reaching down (for the landing flare).
///  - PERCHED: about 13 pages, each a different pose of the sitting bird: the
///    head turned by some angle, the head lifted with the beak at some
///    opening, the tail flicked, the crouch before take-off. To turn the
///    head round, the beak and the mask are squeezed flat as it passes
///    "facing us" and then grow out the other side, the way a cartoon head
///    turns.
///  - A path through the air: ease-out into the perch, so it arrives slowly
///    with its wings raised, and a slow climb away.
/// Everything is painted facing right; a bird going left is the same sticker
/// flipped over (Sprite.Draw's mirror), so no second set is painted.
///
/// WHERE IT SITS: the constructor scans the picture's own pixels near each
/// twig spot for the branch's wood (the brown the painter uses, and the
/// corner-branch stencil says "the branch painted here"), finds the TOP edge
/// of the twig in a column and checks the wood goes on either side of it, so
/// the feet are really on a twig and not in the air beside it (rule 3b). Only
/// spots that pass are perches; if none do (it never happens on the scenes
/// tried), CanBegin is false.
///
/// It is in front of the branches and their leaves (layer 1): a bird that
/// perches ON a twig must be seen in front of the twig's leaves.
/// </summary>
internal sealed class Cardinal : Happening
{
    private const int FlapPages = 16;
    private const float FlapRate = 8.5f;            // wing beats per second
    private const float PerchFor = 4.15f;           // seconds on the twig
    private const float Cruise = 0.34f;             // average flying speed on the way in, U per second
    private const float FlareFor = 0.42f;           // the landing flare at the end of the approach

    private static readonly Color Red = Color.FromArgb(206, 34, 40);
    private static readonly Color RedBreast = Color.FromArgb(230, 64, 56);
    private static readonly Color RedDark = Color.FromArgb(150, 22, 32);
    private static readonly Color Tail = Color.FromArgb(160, 30, 36);
    private static readonly Color Mask = Color.FromArgb(30, 18, 20);
    private static readonly Color BeakC = Color.FromArgb(240, 136, 58);
    private static readonly Color Legs = Color.FromArgb(150, 104, 94);

    private readonly ThanksgivingScenery _s;
    private readonly float _k;                      // pixels in one bird unit (beak to tail is about 0.9 units)
    private readonly int _w, _h, _ox, _by;          // the sprite sheet, where the bird's middle is, and the feet line in it
    private readonly Sprite[] _cruise = new Sprite[FlapPages];
    private readonly Sprite[] _climb = new Sprite[FlapPages];
    private readonly Sprite[][] _flare = new Sprite[3][];       // three stages, two wing positions each
    private readonly Sprite[] _head = new Sprite[5];            // the head turning: 0 = forward, 4 = looking back
    private readonly Sprite _pre, _crouch;
    private readonly Sprite[] _sing = new Sprite[3];            // head lifted, beak closed, half open, wide open
    private readonly Sprite[] _flick = new Sprite[3];
    private readonly List<PointF> _perches = [];                // spots on a twig, found from the pixels

    // This showing's choices.
    private PointF _foot, _start;
    private int _dir = 1;
    private float _approach, _depart, _total = 0.3f;
    private PointF _climbDir;
    private float _climbDistance;

    public override float Seconds => _total;
    public override int Layer => 1;
    public override string? Claims => "branches";
    public override bool CanBegin => _perches.Count > 0;

    public Cardinal(ThanksgivingScenery s)
    {
        _s = s;
        _k = Math.Max(7f, s.U * 0.058f);
        _w = (int)(2.0f * _k) + 4;
        _ox = _w / 2;
        _h = (int)(1.7f * _k) + 4;
        _by = (int)(1.3f * _k) + 2;

        FindPerches();

        // ---- paint the pages (all facing right) ----
        for (int i = 0; i < FlapPages; i++)
        {
            float phi = MathF.Tau * i / FlapPages;
            _cruise[i] = Page(g => Flyer(g, phi, -8f, 0f));
            _climb[i] = Page(g => Flyer(g, phi, 24f, 0f));
        }
        (float tilt, float feet, float[] phi)[] flare = [(20f, 0.3f, [1.3f, 4.4f]), (34f, 0.7f, [1.1f, 4.0f]), (46f, 1f, [1.0f, 3.9f])];
        for (int i = 0; i < 3; i++)
        {
            _flare[i] = new Sprite[2];
            for (int j = 0; j < 2; j++)
            {
                var f = flare[i];
                _flare[i][j] = Page(g => Flyer(g, f.phi[j], f.tilt, f.feet));
            }
        }
        float[] psi = [0f, 0.28f * MathF.PI, 0.55f * MathF.PI, 0.8f * MathF.PI, MathF.PI];
        for (int i = 0; i < 5; i++) { float p = psi[i]; _head[i] = Page(g => Perched(g, p, 0f, 0f, 0f, 0f)); }
        _pre = Page(g => Perched(g, 0f, 0.5f, 0f, 0f, 0f));
        float[] open = [0f, 0.5f, 1f];
        for (int i = 0; i < 3; i++) { float o = open[i]; _sing[i] = Page(g => Perched(g, 0f, 1f, o, 0f, 0f)); }
        _flick[0] = Page(g => Perched(g, 0f, 1f, 0f, 25f, 0f));
        _flick[1] = Page(g => Perched(g, 0f, 1f, 0f, 45f, 0f));
        _flick[2] = Page(g => Perched(g, 0f, 0.4f, 0f, 15f, 0f));
        _crouch = Page(g => Perched(g, 0f, 0f, 0f, 22f, 1f));
    }

    // ------------------------------------------------------------ the perch

    /// <summary>Is the picture's pixel at (x, y) the corner branch's wood? (the stencil says a branch was painted, and the colour is the painter's brown)</summary>
    private bool IsWood(int x, int y)
    {
        if (x < 0 || y < 0 || x >= _s.Width || y >= _s.Height) return false;
        int i = y * _s.Width + x;
        if (_s.OpenOfBranches[i]) return false;
        uint c = _s.Pixels[i];
        int d = Math.Abs((int)((c >> 16) & 0xFF) - 78) + Math.Abs((int)((c >> 8) & 0xFF) - 52) + Math.Abs((int)(c & 0xFF) - 34);
        return d < 60;
    }

    /// <summary>
    /// For every twig spot on screen: look along the pixels near it for the
    /// top edge of some wood, with wood going on both sides of it, and keep
    /// that place (the nearest to the spot) as somewhere to perch.
    /// </summary>
    private void FindPerches()
    {
        int reach = Math.Max(3, (int)(_s.U * 0.025f)), side = Math.Max(2, (int)(_s.U * 0.010f));
        foreach (PointF spot in _s.CornerBranchSpots)
        {
            if (spot.X < _s.Width * 0.05f || spot.X > _s.Width * 0.95f || spot.Y < _k * 1.2f || spot.Y > _s.Height * 0.45f) continue;
            int sx = (int)spot.X, sy = (int)spot.Y;
            int bestX = -1, bestY = 0, bestD = int.MaxValue;
            for (int dx = -reach; dx <= reach; dx++)
            {
                int x = sx + dx;
                for (int y = sy - reach; y <= sy + reach; y++)
                {
                    if (!(IsWood(x, y) && IsWood(x, y + 1) && !IsWood(x, y - 2))) continue;    // the top edge of a bit of wood
                    bool onBothSides = false;
                    for (int dy = -3; dy <= 3 && !onBothSides; dy++)
                        onBothSides = IsWood(x - side, y + dy) && IsWood(x + side, y + dy);
                    if (!onBothSides) continue;
                    int d = Math.Abs(dx);
                    if (d < bestD) { bestD = d; bestX = x; bestY = y; }
                    break;                                                                       // the topmost edge in this column
                }
            }
            if (bestX >= 0) _perches.Add(new PointF(bestX, bestY + 1));
        }
    }

    public override void Begin(Random rng)
    {
        if (_perches.Count == 0) { _total = 0.3f; return; }
        _foot = _perches[rng.Next(_perches.Count)];
        _dir = _foot.X < _s.Width / 2f ? 1 : -1;                 // it faces inward, the way it flew in
        float u = _s.U;

        // The way in: back along a line that climbs up and away from the perch, until it is off the screen.
        var back = new PointF(-_dir * MathF.Cos(0.5f), -MathF.Sin(0.5f));
        float len = u * 0.4f;
        while (len < u * 3f)
        {
            float px = _foot.X + back.X * len, py = _foot.Y + back.Y * len;
            if (px < -1.2f * _k || px > _s.Width + 1.2f * _k || py < -1.4f * _k) break;
            len += 4f;
        }
        _start = new PointF(_foot.X + back.X * len, _foot.Y + back.Y * len);
        _approach = Math.Max(1.2f, len / (Cruise * u));

        // The way out: up and inward, speeding up gently.
        _climbDir = new PointF(_dir * 0.55f, -0.835f);
        float d = 0;
        while (d < u * 3f)
        {
            float px = _foot.X + _climbDir.X * d, py = _foot.Y + _climbDir.Y * d;
            if (px < -1.2f * _k || px > _s.Width + 1.2f * _k || py < -1.4f * _k) break;
            d += 4f;
        }
        _climbDistance = d;
        _depart = Math.Max(1.1f, MathF.Sqrt(2 * d / (0.9f * u)));
        _total = _approach + PerchFor + _depart;
    }

    // ------------------------------------------------------------------ draw

    public override void Draw(FrameBuffer fb, float t)
    {
        if (_perches.Count == 0) return;
        Sprite page;
        float x, y;
        float u = _s.U;

        if (t < _approach)
        {
            float s = t / _approach;
            float e = 1f - MathF.Pow(1f - s, 1.8f);               // quick at first, nearly stopped at the twig
            x = _start.X + (_foot.X - _start.X) * e;
            y = _start.Y + (_foot.Y - _start.Y) * e + u * 0.012f * MathF.Sin(t * 14f) * (1f - e);   // a songbird's rise and dip, dying away on arrival
            float left = _approach - t;
            if (left > FlareFor) page = _cruise[(int)(t * FlapRate * FlapPages) % FlapPages];
            else
            {
                int stage = left > FlareFor * 0.66f ? 0 : left > FlareFor * 0.33f ? 1 : 2;
                page = _flare[stage][(int)(t * 13f) % 2];
            }
        }
        else if (t < _approach + PerchFor)
        {
            float p = t - _approach;
            x = _foot.X; y = _foot.Y;
            if (p < 0.45f) page = _head[0];
            else if (p < 0.85f) page = _head[Math.Min(4, (int)((p - 0.45f) / 0.4f * 5))];             // turning to look behind
            else if (p < 1.55f) page = _head[4];
            else if (p < 1.95f) page = _head[Math.Max(0, 4 - (int)((p - 1.55f) / 0.4f * 5))];        // and back
            else if (p < 2.2f) page = _pre;                                                           // head lifting
            else if (p < 3.55f)
            {
                float q = (p - 2.2f) % 0.5f;
                page = q < 0.1f ? _sing[1] : q < 0.2f ? _sing[2] : q < 0.3f ? _sing[1] : _sing[0];   // three phrases, beak opening and closing
                if (p >= 3.5f) page = _sing[0];
            }
            else if (p < 3.7f) page = _flick[0];
            else if (p < 3.85f) page = _flick[1];
            else if (p < 4.0f) page = _flick[2];
            else page = _crouch;
        }
        else
        {
            float tau = t - _approach - PerchFor;
            float run = _climbDistance * (tau / _depart) * (tau / _depart);                         // speeding up from rest
            x = _foot.X + _climbDir.X * run;
            y = _foot.Y + _climbDir.Y * run;
            page = _climb[(int)(tau * FlapRate * FlapPages) % FlapPages];
        }

        int px = (int)MathF.Round(x) - (_dir > 0 ? _ox : _w - _ox);
        int py = (int)MathF.Round(y) - _by;
        page.Draw(fb, px, py, 1f, null, _dir < 0);
    }

    // ----------------------------------------------------------------- paint

    /// <summary>One page of the flip-book. Bird units: x runs along the bird (facing +x), y runs UP, the feet are the origin.</summary>
    private Sprite Page(Action<Graphics> paint) =>
        Sprite.Paint(_w, _h, g =>
        {
            g.TranslateTransform(_ox, _by);
            g.ScaleTransform(_k, -_k);
            paint(g);
        });

    private static PointF P(float x, float y) => new(x, y);

    private static void Blob(Graphics g, PointF[] pts, Color c, float tension = 0.5f)
    {
        using var path = new GraphicsPath();
        path.AddClosedCurve(pts, tension);
        using var b = new SolidBrush(c);
        g.FillPath(b, path);
    }

    private static void Ellipse(Graphics g, float cx, float cy, float rx, float ry, Color c, float tilt = 0)
    {
        var st = g.Save();
        g.TranslateTransform(cx, cy);
        g.RotateTransform(tilt);
        using var b = new SolidBrush(c);
        g.FillEllipse(b, -rx, -ry, rx * 2, ry * 2);
        g.Restore(st);
    }

    private static void Limb(Graphics g, PointF a, PointF b, float w, Color c)
    {
        using var pen = new Pen(c, w) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        g.DrawLine(pen, a, b);
    }

    /// <summary>The tail: a long wedge from its root along an angle (degrees, y up) with a darker centre line.</summary>
    private static void TailWedge(Graphics g, PointF root, float angleDeg, float len)
    {
        float a = angleDeg * MathF.PI / 180f, ca = MathF.Cos(a), sa = MathF.Sin(a);
        PointF F(float along, float across) => P(root.X + along * ca - across * sa, root.Y + along * sa + across * ca);
        using (var b = new SolidBrush(Tail))
            g.FillPolygon(b, [F(0, -0.035f), F(len * 0.6f, -0.05f), F(len, -0.04f), F(len, 0.04f), F(len * 0.6f, 0.05f), F(0, 0.035f)]);
        Limb(g, F(0.02f, 0), F(len * 0.95f, 0), 0.012f, RedDark);
    }

    /// <summary>The head's face: mask, beak, eye. cs is cos of the turn (1 facing forward, 0 facing us, -1 looking back).</summary>
    private static void Face(Graphics g, PointF c, float cs, float open, float lift)
    {
        float ac = MathF.Abs(cs);
        // The black mask round the beak and down the chin, narrower as the face turns away from us.
        Ellipse(g, c.X + 0.05f * cs, c.Y - 0.004f, 0.026f + 0.034f * ac, 0.052f, Mask);
        Ellipse(g, c.X + 0.032f * cs, c.Y - 0.058f, 0.024f + 0.026f * ac, 0.046f, Mask);
        // The beak: stout, orange-red, two halves that part when it sings.
        PointF b = P(c.X + 0.075f * cs, c.Y - 0.006f);
        float blen = 0.085f * cs, up = lift * 0.012f;
        using (var upper = new SolidBrush(BeakC))
            g.FillPolygon(upper, [P(b.X, b.Y + 0.034f), P(b.X + blen, b.Y + 0.004f + up + open * 0.028f), P(b.X, b.Y - 0.004f)]);
        if (open > 0.05f)
            using (var inside = new SolidBrush(Color.FromArgb(110, 20, 26)))
                g.FillPolygon(inside, [P(b.X, b.Y - 0.004f), P(b.X + blen * 0.85f, b.Y + 0.004f + up + open * 0.026f), P(b.X + blen * 0.85f, b.Y - 0.002f + up - open * 0.034f)]);
        using (var lower = new SolidBrush(Color.FromArgb(226, 118, 48)))
            g.FillPolygon(lower, [P(b.X, b.Y - 0.004f), P(b.X + blen * 0.9f, b.Y - 0.002f + up - open * 0.040f), P(b.X, b.Y - 0.036f)]);
        // The eye: dark in the mask, with a tiny bright glint, the storybook touch.
        Ellipse(g, c.X + 0.020f * cs, c.Y + 0.022f, 0.018f, 0.018f, Color.FromArgb(12, 6, 8));
        Ellipse(g, c.X + 0.026f * cs, c.Y + 0.030f, 0.007f, 0.007f, Color.FromArgb(250, 250, 244));
    }

    /// <summary>The bird sitting. psi = how far the head is turned (0 forward, pi looking back), lift = head raised for song (0 to 1), open = beak (0 to 1), flick = tail raised (degrees), crouch = squatting to spring (0 to 1).</summary>
    private static void Perched(Graphics g, float psi, float lift, float open, float flick, float crouch)
    {
        float cs = MathF.Cos(psi);
        float drop = 0.05f * crouch;

        // The tail, behind everything: down and back, raised by the flick.
        TailWedge(g, P(-0.15f, 0.25f - drop), 215f - flick - 18f * crouch, 0.38f);

        // Legs and feet: thin, gripping the twig (the feet are the origin).
        foreach (float hx in (float[])[-0.03f, 0.05f])
        {
            Limb(g, P(hx, 0.21f - drop), P(hx + 0.01f, 0.01f), 0.028f, Legs);
            Limb(g, P(hx + 0.01f, 0.01f), P(hx + 0.08f, 0.0f), 0.02f, Legs);
            Limb(g, P(hx + 0.01f, 0.01f), P(hx - 0.04f, 0.0f), 0.018f, Legs);
        }

        // The body: a plump oval tilted nose-up, a lighter breast that swells a little with song.
        Ellipse(g, 0.0f, 0.31f - drop, 0.215f, 0.150f, Red, 28f);
        Ellipse(g, 0.075f, 0.27f - drop, 0.135f * (1f + 0.1f * lift), 0.105f * (1f + 0.1f * lift), RedBreast, 28f);

        // The folded wing along the side, darker, with feather lines.
        Blob(g, [P(0.10f, 0.38f - drop), P(0.0f, 0.43f - drop), P(-0.15f, 0.35f - drop), P(-0.27f, 0.21f - drop), P(-0.21f, 0.165f - drop), P(-0.05f, 0.24f - drop), P(0.08f, 0.31f - drop)], RedDark, 0.4f);
        for (int i = 0; i < 3; i++)
            Limb(g, P(-0.03f - i * 0.065f, 0.33f - drop - i * 0.035f), P(-0.10f - i * 0.065f, 0.25f - drop - i * 0.035f), 0.012f, Color.FromArgb(118, 16, 26));

        // The neck joins head to body; the head is a round ball with a pointed crest.
        PointF head = P(0.175f - 0.035f * lift, 0.47f - drop + 0.03f * lift);
        Ellipse(g, 0.12f, 0.42f - drop, 0.10f, 0.095f, Red, 40f);
        Ellipse(g, head.X, head.Y, 0.100f, 0.094f, Red);
        Blob(g, [P(head.X - 0.10f * cs - 0.012f * lift * cs, head.Y + 0.205f), P(head.X + 0.045f * cs, head.Y + 0.080f), P(head.X - 0.02f * cs, head.Y + 0.05f), P(head.X - 0.09f * cs, head.Y + 0.045f)], Red, 0.35f);
        Face(g, head, cs, open, lift);
    }

    /// <summary>
    /// The bird flying, the origin at the middle of its body. phi is where the wings are in their beat (0 to 2 pi), tilt the body's nose-up angle in degrees, feet how far the feet reach down for a landing (0 tucked, 1 out).
    /// </summary>
    private static void Flyer(Graphics g, float phi, float tilt, float feet)
    {
        g.TranslateTransform(0, 0.33f);                          // the flight pages hang the body's middle 0.33 units above the feet line
        g.RotateTransform(tilt);

        void Wing(float phase, Color c, float shift)
        {
            // The blade swings up-and-back then down-and-back; at the middle of the beat it points toward us, so it looks shorter.
            float sinP = MathF.Sin(phase), cosP = MathF.Cos(phase);
            float theta = (175f - 70f * sinP) * MathF.PI / 180f;
            float len = 0.36f + 0.18f * MathF.Abs(sinP);
            float bend = -24f * cosP * MathF.PI / 180f;          // the tip lags behind the stroke
            float ct = MathF.Cos(theta), st = MathF.Sin(theta);
            PointF S = P(0.05f + shift, 0.07f);
            PointF Q(float along, float across)
            {
                // Beyond half way the blade bends by "bend" about its middle.
                float a = along, b = across;
                if (a > 0.5f * len)
                {
                    float da = a - 0.5f * len, cb = MathF.Cos(bend), sb = MathF.Sin(bend);
                    a = 0.5f * len + da * cb - b * sb;
                    b = da * sb + b * cb;
                }
                return P(S.X + a * ct - b * st, S.Y + a * st + b * ct);
            }
            PointF[] blade = [Q(0, 0.055f), Q(0.45f * len, 0.07f), Q(len, 0.02f), Q(0.96f * len, -0.035f), Q(0.7f * len, -0.085f), Q(0.35f * len, -0.11f), Q(0, -0.095f)];
            Blob(g, blade, c, 0.3f);
            // Feather lines near the tip, a little darker.
            for (int i = 0; i < 3; i++)
                Limb(g, Q(len * (0.55f + 0.12f * i), -0.03f), Q(len * (0.88f + 0.03f * i), -0.02f - 0.012f * i), 0.011f, Color.FromArgb(110, 14, 24));
        }

        // The far wing (a little behind and darker), the tail, the body, the head, then the near wing on top.
        Wing(phi - 0.35f, Color.FromArgb(128, 16, 28), -0.02f);
        TailWedge(g, P(-0.19f, 0.0f), 184f + 6f * MathF.Sin(phi), 0.34f);

        if (feet > 0.05f)                                         // landing: legs reach down and forward
            foreach (float hx in (float[])[-0.02f, 0.06f])
            {
                PointF knee = P(hx + 0.03f * feet, -0.10f - 0.10f * feet), foot = P(hx + 0.07f * feet, -0.10f - 0.22f * feet);
                Limb(g, P(hx, -0.08f), knee, 0.026f, Legs);
                Limb(g, knee, foot, 0.02f, Legs);
                Limb(g, foot, P(foot.X + 0.07f, foot.Y - 0.01f), 0.018f, Legs);
            }
        else
            Limb(g, P(0.0f, -0.1f), P(0.08f, -0.12f), 0.03f, Color.FromArgb(120, 20, 30));       // feet tucked up under the belly

        Ellipse(g, 0.0f, 0.0f, 0.235f, 0.125f, Red, 2f);
        Ellipse(g, 0.06f, -0.03f, 0.13f, 0.08f, RedBreast, 2f);
        PointF head = P(0.235f, 0.05f);
        Ellipse(g, head.X - 0.05f, head.Y - 0.01f, 0.08f, 0.075f, Red);
        Ellipse(g, head.X, head.Y, 0.088f, 0.084f, Red);
        Blob(g, [P(head.X - 0.17f, head.Y + 0.065f), P(head.X + 0.01f, head.Y + 0.075f), P(head.X - 0.03f, head.Y + 0.03f), P(head.X - 0.09f, head.Y + 0.035f)], Red, 0.35f);   // the crest, swept back in flight
        Face(g, head, 1f, 0f, 0f);

        Wing(phi, Red, 0f);
    }
}
