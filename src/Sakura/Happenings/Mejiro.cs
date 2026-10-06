using System.Drawing.Drawing2D;
using Peckworks.Screensavers.Core;

namespace Sakura.Happenings;

/// <summary>
/// A pair of mejiro (Japanese white-eyes) visit the blossom. They are tiny
/// olive-green birds with a bright white ring round each eye, and in Japan
/// they are the bird that always turns up when the plum and cherry trees
/// flower. Here they flutter in from one side, land together on a blossom
/// cluster, poke their beaks into the flowers, one hops to a neighbouring
/// cluster and pokes there, they turn to look at each other, and then they
/// flit away.
///
/// FEYNMAN VERSION: every bird is a flip-book of small pictures, painted
/// once in the constructor, and Draw only picks the right page for the
/// moment. There are three little books:
///   flying    16 pages of one wing beat (the wing swings up and down, with a
///             faint ghost of where it just was, which reads as blur);
///   dipping   6 pages of the bird leaning forward more and more, beak into
///             the flower, so a dip plays as 0 1 2 3 4 5 4 3 2 1 0;
///   looking   5 pages of the head turning from "looking ahead" through
///             "face on, two eyes" to "looking right behind".
/// Each book is painted twice, once facing right and once mirrored, so a bird
/// can face either way.
///
/// Where they sit: the painter lists every blossom cluster on the branches.
/// In the constructor we keep only the ones high on the screen where the
/// pixel under a bird's feet really is blossom or wood (not sky): a blossom
/// pixel is pink or white (red at least as strong as blue), the sky is
/// blue (blue clearly stronger than red). Begin then picks one at random.
///
/// The birds are in FRONT of the branches (Layer 1), so no stencil.
/// </summary>
internal sealed class Mejiro : Happening
{
    private const float Total = 12f;
    private const int FlyPages = 16, DipPages = 6, LookPages = 5;

    // How far each of the five head pages is turned: 1 = looking the way the body faces,
    // 0 = looking at us, -1 = looking straight behind.
    private static readonly float[] LookD = [1f, 0.55f, 0f, -0.55f, -1f];

    private static readonly Color Olive = Color.FromArgb(116, 152, 46);
    private static readonly Color OliveDark = Color.FromArgb(84, 118, 38);
    private static readonly Color OliveLight = Color.FromArgb(160, 190, 78);
    private static readonly Color Throat = Color.FromArgb(210, 218, 80);
    private static readonly Color Belly = Color.FromArgb(232, 230, 214);
    private static readonly Color Flank = Color.FromArgb(220, 196, 150);
    private static readonly Color Beak = Color.FromArgb(78, 66, 62);
    private static readonly Color EyeDark = Color.FromArgb(26, 20, 24);
    private static readonly Color Feet = Color.FromArgb(128, 104, 96);

    private readonly Scenery _s;
    private readonly float _k;                       // pixels per drawing unit: one unit is the bird's whole length (0.03 U)
    private readonly int _w, _h, _gy;                // sprite size, and the row of the bird's feet
    private readonly float _cx;                      // the column of the bird's middle
    private readonly Sprite[][] _fly = new Sprite[2][];   // [0] faces right, [1] faces left
    private readonly Sprite[][] _dip = new Sprite[2][];
    private readonly Sprite[][] _look = new Sprite[2][];
    private readonly List<PointF> _cands = [];       // blossom clusters a bird could stand on

    // ---- this showing (rolled in Begin) ----
    private bool _ok;
    private bool _fromLeft;                          // which side they arrive from
    private readonly PointF[] _start = new PointF[2];    // where each bird comes from (off screen)
    private readonly PointF[] _land = new PointF[2];     // where each lands
    private PointF _hopTo;                           // where bird A hops to
    private PointF _exitA, _exitB;                   // where each flits off to (off screen)
    private int _faceA2;                             // which way A faces after its hop
    private bool _turnBack0, _turnBack1;             // is the other bird behind me when I look at it?

    // ---- the timetable, in seconds (A is the first bird, B the second) ----
    private static readonly float[] PokesA1 = [2.7f, 3.3f, 3.9f];
    private static readonly float[] PokesA2 = [5.8f, 6.4f, 7.0f];
    private static readonly float[] PokesB = [3.9f, 4.5f, 5.1f];

    public override float Seconds => _ok ? Total : 0.1f;
    public override string? Claims => "branches";
    public override int Layer => 1;
    public override bool CanBegin => _cands.Count > 0;

    public Mejiro(Scenery s)
    {
        _s = s;
        _k = Math.Max(11f, s.U * 0.03f);
        _w = (int)(1.5f * _k) + 6;
        _h = (int)(1.3f * _k) + 6;
        _cx = _w / 2f;
        _gy = _h - 3;

        for (int f = 0; f < 2; f++)
        {
            bool flip = f == 1;
            _fly[f] = new Sprite[FlyPages];
            for (int i = 0; i < FlyPages; i++) _fly[f][i] = Page(flip, 0f, 1f, true, i / (float)FlyPages);
            _dip[f] = new Sprite[DipPages];
            for (int i = 0; i < DipPages; i++) _dip[f][i] = Page(flip, i / (DipPages - 1f), 1f, false, 0f);
            _look[f] = new Sprite[LookPages];
            for (int i = 0; i < LookPages; i++) _look[f][i] = Page(flip, 0f, LookD[i], false, 0f);
        }

        // Clusters high on the screen with blossom or wood under the feet and a little beside them.
        foreach (var p in s.BranchSpots)
        {
            if (p.Y < s.Height * 0.06f || p.Y > s.Height * 0.6f) continue;
            if (p.X < s.Width * 0.03f || p.X > s.Width * 0.97f) continue;
            float fy = p.Y + 0.006f * s.U, r = 0.01f * s.U;
            if (Twig(p.X, fy) && Twig(p.X - r, fy) && Twig(p.X + r, fy)) _cands.Add(p);
        }
    }

    /// <summary>Is the backdrop pixel here blossom or wood? Sky is bluer than it is red; pink, white and brown wood are not.</summary>
    private bool Twig(float x, float y)
    {
        int ix = (int)x, iy = (int)y;
        if (ix < 0 || iy < 0 || ix >= _s.Width || iy >= _s.Height) return false;
        uint p = _s.Pixels[iy * _s.Width + ix];
        return (int)((p >> 16) & 0xFF) >= (int)(p & 0xFF) - 2;
    }

    public override void Begin(Random rng)
    {
        _ok = false;
        if (_cands.Count == 0) return;
        float u = _s.U, w = _s.Width, h = _s.Height;
        PointF p1 = _cands[rng.Next(_cands.Count)];
        _fromLeft = rng.Next(2) == 0;
        int face = _fromLeft ? 1 : -1;
        float fy = 0.006f * u;
        _land[0] = new PointF(p1.X, p1.Y + fy);

        // Bird B stands on the same cluster, a little to the side (try both sides, then give up and overlap a bit).
        float side = rng.Next(2) == 0 ? 1 : -1;
        PointF b = new(p1.X - side * 0.04f * u, p1.Y + fy);
        if (!Twig(b.X, b.Y)) b = new PointF(p1.X + side * 0.04f * u, p1.Y + fy);
        if (!Twig(b.X, b.Y)) b = new PointF(p1.X - face * 0.03f * u, p1.Y + fy);
        _land[1] = b;

        // The neighbouring cluster A hops to: a random one between 0.05 and 0.22 U away.
        var near = _cands.Where(c => { float d = MathF.Sqrt((c.X - p1.X) * (c.X - p1.X) + (c.Y - p1.Y) * (c.Y - p1.Y)); return d > 0.05f * u && d < 0.22f * u; }).ToList();
        if (near.Count > 0) { var c = near[rng.Next(near.Count)]; _hopTo = new PointF(c.X, c.Y + fy); }
        else
        {
            _hopTo = new PointF(p1.X + side * 0.05f * u, p1.Y + fy - 0.012f * u);
            if (!Twig(_hopTo.X, _hopTo.Y)) _hopTo = new PointF(p1.X - side * 0.05f * u, p1.Y + fy - 0.012f * u);
        }
        _faceA2 = Math.Abs(_hopTo.X - p1.X) < 0.01f * u ? face : (_hopTo.X > p1.X ? 1 : -1);

        // Where they come from: off the side, above the perch.
        float sx = _fromLeft ? -0.06f * w : 1.06f * w;
        _start[0] = new PointF(sx, Math.Max(h * 0.02f, _land[0].Y - (0.12f + 0.12f * (float)rng.NextDouble()) * u));
        _start[1] = new PointF(sx + (_fromLeft ? -1 : 1) * 0.05f * u, Math.Max(h * 0.02f, _land[1].Y - (0.1f + 0.12f * (float)rng.NextDouble()) * u));
        float ex = _fromLeft ? 1.08f * w : -0.08f * w;
        _exitA = new PointF(ex, _hopTo.Y - (0.22f + 0.12f * (float)rng.NextDouble()) * u);
        _exitB = new PointF(ex, _land[1].Y - (0.25f + 0.12f * (float)rng.NextDouble()) * u);

        // Is the other bird behind me (so I must turn my head right round)? Bird B faces the way it flew in;
        // A faces _faceA2 after its hop.
        _turnBack1 = (_hopTo.X - _land[1].X) * face < 0;
        _turnBack0 = (_land[1].X - _hopTo.X) * _faceA2 < 0;
        _ok = true;
    }

    // ================================================================ drawing

    public override void Draw(FrameBuffer fb, float t)
    {
        if (!_ok) return;
        float u = _s.U;
        int face = _fromLeft ? 1 : -1;

        // ---- bird A (the first in, the one that hops) ----
        {
            const float e0 = 0f, e1 = 2.1f, hop0 = 4.7f, hop1 = 5.15f, x0 = 9.5f, x1 = 11.7f;
            PointF p;
            Sprite sp;
            if (t < e1)
            {
                p = Entry(0, t, e0, e1, u, out int page);
                sp = _fly[F(face)][page];
            }
            else if (t < hop0)
            {
                p = _land[0];
                sp = Perched(face, Dips(t, PokesA1), 0);
            }
            else if (t < hop1)
            {
                float q = (t - hop0) / (hop1 - hop0);
                p = new PointF(_land[0].X + (_hopTo.X - _land[0].X) * Smooth(q),
                               _land[0].Y + (_hopTo.Y - _land[0].Y) * Smooth(q) - 0.045f * u * MathF.Sin(MathF.PI * q));
                sp = _fly[F(_faceA2)][FlyIndex((t - hop0) * 16f)];
            }
            else if (t < x0)
            {
                p = _hopTo;
                int look = Look(t, 7.7f, 8.3f, 9.3f, 9.5f, _turnBack0 ? 4 : 2);
                sp = Perched(_faceA2, Dips(t, PokesA2), look);
            }
            else
            {
                p = Exit(_hopTo, _exitA, t, x0, x1, 0);
                sp = _fly[F(Math.Sign(_exitA.X - _hopTo.X))][FlyIndex((t - x0) * 12f)];
            }
            Stamp(fb, sp, p);
        }

        // ---- bird B (lands beside A, pokes, then turns to look at A) ----
        {
            const float e0 = 0.45f, e1 = 2.6f, x0 = 9.9f, x1 = 12.0f;
            PointF p;
            Sprite sp;
            if (t < e0) { /* still off screen, waiting to fly in */ }
            else if (t < e1)
            {
                p = Entry(1, t, e0, e1, u, out int page);
                sp = _fly[F(face)][page];
                Stamp(fb, sp, p);
            }
            else if (t < x0)
            {
                // Glance round (2 pages and back), poke three times, then turn toward A and hold.
                int look = t < 4.0f ? Look(t, 3.0f, 3.35f, 3.4f, 3.75f, 1)
                         : Look(t, 6.1f, 6.8f, 8.7f, 9.4f, _turnBack1 ? 4 : 2);
                sp = Perched(face, Dips(t, PokesB), look);
                Stamp(fb, sp, _land[1]);
            }
            else
            {
                p = Exit(_land[1], _exitB, t, x0, x1, 1);
                sp = _fly[F(Math.Sign(_exitB.X - _land[1].X))][FlyIndex((t - x0) * 12f)];
                Stamp(fb, sp, p);
            }
        }
    }

    private static int F(int face) => face >= 0 ? 0 : 1;

    private static int FlyIndex(float page) => ((int)MathF.Floor(page) % FlyPages + FlyPages) % FlyPages;

    /// <summary>Comes in from off screen and lands: eases out (fast then gentle), with a looping flutter that dies away, and wings that beat faster on landing.</summary>
    private PointF Entry(int who, float t, float e0, float e1, float u, out int page)
    {
        float q = Math.Clamp((t - e0) / (e1 - e0), 0f, 1f);
        float e = 1 - (1 - q) * (1 - q);
        float x = _start[who].X + (_land[who].X - _start[who].X) * e;
        float y = _start[who].Y + (_land[who].Y - _start[who].Y) * e
                  - 0.03f * u * MathF.Sin(MathF.PI * q)
                  + 0.014f * u * (1 - q) * MathF.Sin(q * 22f + who * 2f);
        float tau = t - e0;
        page = FlyIndex((8f * tau + 4f * tau * q) * FlyPages);
        return new PointF(x, y);
    }

    /// <summary>Takes off: starts from rest (ease-in), rises and weaves, and is off screen by the end.</summary>
    private static PointF Exit(PointF from, PointF to, float t, float t0, float t1, int who)
    {
        float q = Math.Clamp((t - t0) / (t1 - t0), 0f, 1f);
        float e = q * q;
        float wobble = MathF.Sin(q * 14f + who * 1.7f) * 8f * q * (1 - q);
        return new PointF(from.X + (to.X - from.X) * e, from.Y + (to.Y - from.Y) * e + wobble);
    }

    /// <summary>How far the bird is dipping its beak at time t: 0 (upright) to 5 (deep in the flower), as a page number.</summary>
    private static int Dips(float t, float[] pokes)
    {
        float amt = 0;
        foreach (float ts in pokes)
            if (t >= ts && t < ts + 0.4f) amt = Math.Max(amt, MathF.Sin(MathF.PI * (t - ts) / 0.4f));
        return (int)MathF.Round(amt * (DipPages - 1));
    }

    /// <summary>Head-turn page: rises over a..b, holds to c, returns by d. "target" is the page it reaches (0 to 4).</summary>
    private static int Look(float t, float a, float b, float c, float d, int target)
    {
        float v = Smooth((t - a) / (b - a)) * (1 - Smooth((t - c) / (d - c)));
        return (int)MathF.Round(v * target);
    }

    private Sprite Perched(int face, int dip, int look) => dip > 0 ? _dip[F(face)][dip] : _look[F(face)][look];

    /// <summary>Stamps a bird whole-pixel placed so its feet are at p (rounded once).</summary>
    private void Stamp(FrameBuffer fb, Sprite sp, PointF p) =>
        sp.Draw(fb, (int)MathF.Round(p.X - _cx), (int)MathF.Round(p.Y - _gy));

    // ================================================================ painting one page

    /// <summary>
    /// Paints one bird, in "bird units" (one unit = the bird's whole length),
    /// with its feet at (0, 0) and up being negative y. The drawing kit is
    /// told to scale everything by _k, so the numbers below are fractions of
    /// the bird, never pixels. Think of drawing on a sheet of graph paper and
    /// then photocopying it at the size you need.
    /// </summary>
    /// <param name="dip">0 upright to 1 beak right down in the flower (leans the whole body forward).</param>
    /// <param name="d">Head turn: 1 looking ahead, 0 face on, -1 looking behind.</param>
    /// <param name="flying">Wings out and feet tucked.</param>
    /// <param name="phase">Where in the wing beat, 0 to 1 (flying only).</param>
    private Sprite Page(bool flip, float dip, float d, bool flying, float phase)
    {
        return Sprite.Paint(_w, _h, g =>
        {
            if (flip) { g.TranslateTransform(_w, 0); g.ScaleTransform(-1, 1); }
            g.TranslateTransform(_cx, _gy);
            g.ScaleTransform(_k, _k);

            if (!flying)
            {
                // Two thin legs, drawn before the lean so they stay planted on the branch.
                using var leg = new Pen(Feet, 0.045f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                g.DrawLine(leg, 0.02f, -0.1f, 0.0f, 0.0f);
                g.DrawLine(leg, 0.11f, -0.1f, 0.13f, 0.0f);
            }

            // Lean forward about the feet. Positive turns clockwise: the top moves toward the beak.
            g.TranslateTransform(0, -0.12f);
            g.RotateTransform(flying ? 14f : dip * 40f);
            g.TranslateTransform(0, 0.12f);

            float wingA = 18f + 68f * MathF.Sin(MathF.Tau * phase);          // wing angle: up is positive
            float ghostA = 18f + 68f * MathF.Sin(MathF.Tau * (phase - 0.09f));
            if (flying) Wing(g, wingA - 8f, Color.FromArgb(150, OliveDark), 0.9f);   // far wing, dimmer and a little out of step

            // Tail: a short dark wedge, pointing back and down.
            using (var tail = new SolidBrush(OliveDark))
                g.FillPolygon(tail, [new PointF(-0.2f, -0.36f), new PointF(-0.56f, -0.12f), new PointF(-0.5f, -0.03f), new PointF(-0.16f, -0.2f)]);

            // Body: olive back, pale grey belly, a buff blush on the flank.
            using (var b = new SolidBrush(Olive)) g.FillEllipse(b, -0.34f, -0.53f, 0.62f, 0.52f);
            using (var b = new SolidBrush(Belly)) g.FillEllipse(b, -0.22f, -0.36f, 0.54f, 0.34f);
            using (var b = new SolidBrush(Color.FromArgb(160, Flank))) g.FillEllipse(b, -0.2f, -0.2f, 0.2f, 0.12f);

            // Wing (folded when perched: a darker leaf lying along the body).
            if (flying)
            {
                Wing(g, ghostA, Color.FromArgb(70, OliveDark), 1.0f);       // the blur
                Wing(g, wingA, OliveDark, 1.0f);
            }
            else
            {
                var st = g.Save();
                g.TranslateTransform(-0.1f, -0.32f);
                g.RotateTransform(22f);
                using (var b = new SolidBrush(OliveDark)) g.FillEllipse(b, -0.22f, -0.1f, 0.42f, 0.2f);
                using (var b = new SolidBrush(OliveLight)) g.FillEllipse(b, -0.2f, -0.09f, 0.24f, 0.07f);
                g.Restore(st);
            }

            Head(g, d);
        });
    }

    /// <summary>One wing: a leaf shape from the shoulder, swung to angle a (degrees, up positive; 0 points straight back).</summary>
    private static void Wing(Graphics g, float a, Color color, float scale)
    {
        float r = a * MathF.PI / 180f;
        float dx = -MathF.Cos(r) * 0.85f, dy = -MathF.Sin(r);
        float len = MathF.Sqrt(dx * dx + dy * dy);
        dx /= len; dy /= len;
        float px = -dy, py = dx;                 // sideways
        PointF sh = new(-0.02f, -0.38f);
        float L = 0.56f * scale;
        PointF at(float along, float side) => new(sh.X + dx * along + px * side, sh.Y + dy * along + py * side);
        PointF[] leaf = [at(0, 0.09f), at(L * 0.45f, 0.13f), at(L, 0.0f), at(L * 0.45f, -0.13f), at(0, -0.09f)];
        using var b = new SolidBrush(color);
        g.FillClosedCurve(b, leaf, FillMode.Winding, 0.6f);
        using var lite = new SolidBrush(Color.FromArgb(color.A * 2 / 3, OliveLight));
        g.FillClosedCurve(lite, [at(L * 0.25f, 0.03f), at(L * 0.6f, 0.05f), at(L * 0.9f, 0.0f), at(L * 0.6f, -0.01f)], FillMode.Winding, 0.6f);
    }

    /// <summary>The head: olive cap, yellow-green throat, a short dark beak, and the famous white eye-ring.</summary>
    private static void Head(Graphics g, float d)
    {
        float hcx = 0.20f + (d - 1f) * 0.06f, hcy = -0.53f, hr = 0.215f;
        using (var b = new SolidBrush(Olive)) g.FillEllipse(b, hcx - hr, hcy - hr, hr * 2, hr * 2);
        // Yellow-green throat, on the side the face points.
        using (var b = new SolidBrush(Throat)) g.FillEllipse(b, hcx + d * 0.06f - 0.13f, hcy + 0.05f, 0.26f, 0.15f);

        // Beak: seen from the side it is a small pointed wedge; face on it is a tiny downward point.
        using (var b = new SolidBrush(Beak))
        {
            if (MathF.Abs(d) < 0.3f)
                g.FillPolygon(b, [new PointF(hcx - 0.04f, hcy + 0.07f), new PointF(hcx + 0.04f, hcy + 0.07f), new PointF(hcx, hcy + 0.14f)]);
            else
            {
                float s = d > 0 ? 1 : -1, len = 0.13f * MathF.Abs(d);
                float bx = hcx + s * (0.17f + 0.02f * MathF.Abs(d));
                g.FillPolygon(b, [new PointF(bx, hcy - 0.01f), new PointF(bx + s * (len + 0.02f), hcy + 0.055f), new PointF(bx, hcy + 0.075f)]);
            }
        }

        // Cheek blush (very soft).
        using (var b = new SolidBrush(Color.FromArgb(80, 246, 150, 140)))
        {
            if (MathF.Abs(d) < 0.3f) { g.FillEllipse(b, hcx - 0.19f, hcy + 0.03f, 0.1f, 0.07f); g.FillEllipse(b, hcx + 0.09f, hcy + 0.03f, 0.1f, 0.07f); }
            else g.FillEllipse(b, hcx - d * 0.05f - 0.05f, hcy + 0.05f, 0.1f, 0.07f);
        }

        // Eyes: a crisp white ring, a big dark eye, a tiny highlight.
        if (MathF.Abs(d) < 0.3f) { Eye(g, hcx - 0.085f, hcy - 0.03f, 0.082f, 0f); Eye(g, hcx + 0.085f, hcy - 0.03f, 0.082f, 0f); }
        else Eye(g, hcx + d * 0.12f, hcy - 0.03f, 0.1f, d);
    }

    private static void Eye(Graphics g, float x, float y, float ring, float d)
    {
        using (var w = new SolidBrush(Color.White)) g.FillEllipse(w, x - ring, y - ring, ring * 2, ring * 2);
        float e = ring * 0.66f;
        using (var k = new SolidBrush(EyeDark)) g.FillEllipse(k, x - e, y - e, e * 2, e * 2);
        float hl = ring * 0.24f;
        using (var w = new SolidBrush(Color.White)) g.FillEllipse(w, x - e * 0.1f + d * 0.01f, y - e * 0.7f, hl * 2, hl * 2);
    }
}
