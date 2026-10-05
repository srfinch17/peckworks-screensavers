using System.Drawing.Drawing2D;
using Peckworks.Screensavers.Core;

namespace Halloween.Happenings;

/// <summary>
/// A sickly green hand thrusts up out of the ground beside a tombstone,
/// clods of dirt jumping up and falling back as it breaks through. The
/// fingers then curl and uncurl as if grasping at the air, the wrist sways,
/// and it sinks back into the earth.
///
/// THE TRICK for coming out of the ground: every sprite has its bottom edge
/// exactly on the ground line, and anything painted below that edge simply
/// does not exist (a sprite is only as big as its sheet). So for the rising
/// we paint 24 pictures of the same open hand, each shifted a little further
/// DOWN inside its sheet. The first has the hand fully up, the last has it
/// entirely below the edge. Stamping them in order is a hand climbing out,
/// with no stencil needed. A small mound of dark earth is painted at the
/// bottom of every sheet to hide the straight cut, like a heap of soil
/// around a bulb coming up in spring.
///
/// Then a second flip-book of 16 poses, loops of "grasping": the fingers
/// close and open (curl from 0 to 1 and back) while the whole hand tips a
/// little left and right from the wrist. Pose 0 is the same picture as the
/// first rising page, so the changeover has no jump.
///
/// Sizes are in "hand units": 1 hand unit is the hand's full height, 0.05 U.
/// In those units the forearm runs 0 to 0.5, the palm 0.5 to 0.72, and the
/// fingertips reach about 1.
/// </summary>
internal sealed class ZombieHand : Happening
{
    private const int RisePages = 24;
    private const int GraspPages = 16;
    private const int Clods = 7;

    private static readonly Color Skin = Color.FromArgb(150, 190, 130);
    private static readonly Color Outline = Color.FromArgb(44, 66, 42);

    private readonly HalloweenScenery _s;
    private readonly Sprite[] _rise = new Sprite[RisePages];
    private readonly Sprite[] _grasp = new Sprite[GraspPages];
    private readonly Sprite _clodBig, _clodSmall;
    private readonly int _w, _h;
    private float _x, _groundY;                                  // where the hand comes up
    private readonly float[] _clodStart = new float[Clods];      // when each clod is thrown, in seconds
    private readonly float[] _clodVx = new float[Clods];         // sideways speed (fractions of U per second)
    private readonly float[] _clodVy = new float[Clods];         // upward speed
    private readonly float[] _clodDx = new float[Clods];         // where along the mound it starts

    public override float Seconds => 9f;
    // One happening at a time among the stones and pumpkins: they are drawn
    // in the order they began, with no idea of who stands in front, so a cat
    // and a zombie hand on the same spot would be drawn through each other.
    public override string? Claims => "graveyard";

    public ZombieHand(HalloweenScenery s)
    {
        _s = s;
        float hu = Math.Max(16f, s.U * 0.068f);                  // one hand unit in pixels, never tiny
        _w = (int)(hu * 1.0f);
        _h = (int)(hu * 1.1f);
        for (int i = 0; i < RisePages; i++)
            _rise[i] = PaintHand(_w, _h, hu, shift: i / (float)(RisePages - 1), curl: 0f, sway: 0f);
        for (int i = 0; i < GraspPages; i++)
        {
            float a = i * MathF.Tau / GraspPages;
            // curl: 0 (open) -> 1 (fist) -> 0.  sway: tips left then right once per loop. Both are 0 at pose 0.
            _grasp[i] = PaintHand(_w, _h, hu, shift: 0f, curl: 0.5f - 0.5f * MathF.Cos(a), sway: 0.11f * MathF.Sin(a));
        }

        int big = Math.Max(3, (int)(s.U * 0.011f)), small = Math.Max(2, (int)(s.U * 0.007f));
        _clodBig = PaintClod(big);
        _clodSmall = PaintClod(small);
    }

    /// <summary>A lump of earth: a dark brown dot with a lighter top edge where it catches the moonlight.</summary>
    private static Sprite PaintClod(int d) => Sprite.Paint(d, d, g =>
    {
        using var earth = new SolidBrush(Color.FromArgb(54, 38, 40));
        using var lit = new SolidBrush(Color.FromArgb(104, 80, 76));
        g.FillEllipse(earth, 0, 0, d, d);
        g.FillEllipse(lit, d * 0.15f, d * 0.05f, d * 0.5f, d * 0.4f);
    });

    public override void Begin(Random rng)
    {
        var stone = _s.Tombstones[rng.Next(_s.Tombstones.Count)];
        // Just to one side of the stone: 0.03 to 0.04 U away, left or right.
        float side = rng.Next(2) == 0 ? -1f : 1f;
        _x = stone.Foot.X + side * _s.U * (0.03f + 0.01f * (float)rng.NextDouble());
        _groundY = _s.Ground.YAt(_x);
        for (int i = 0; i < Clods; i++)
        {
            _clodStart[i] = 0.55f + 0.5f * (float)rng.NextDouble();        // thrown as the hand breaks through
            _clodVx[i] = (float)(rng.NextDouble() * 2 - 1) * 0.07f;
            _clodVy[i] = 0.12f + 0.09f * (float)rng.NextDouble();
            _clodDx[i] = (float)(rng.NextDouble() * 2 - 1) * 0.012f;
        }
    }

    // ---- painting the hand ----

    /// <summary>
    /// Paints one picture of the hand. "shift" is how far down it has sunk
    /// (0 = fully up, 1 = completely below ground). "curl" is how closed the
    /// fingers are. "sway" is how far the whole hand leans from the wrist,
    /// in radians (positive leans right).
    ///
    /// Two passes: first everything in the dark outline color, drawn fatter,
    /// then everything in green on top. Drawing all the outlines first means
    /// the fingers and palm melt into one silhouette with one clean border,
    /// not a dark line across every finger's base.
    /// </summary>
    private static Sprite PaintHand(int w, int h, float hu, float shift, float curl, float sway)
    {
        return Sprite.Paint(w, h, g =>
        {
            float ox = w / 2f, oy = h;                    // the ground line: bottom middle of the sheet
            float ca = MathF.Cos(sway), sa = MathF.Sin(sway);

            // Hand units (x across, yUp upward from the ground) to sheet pixels: lean from the wrist, then sink.
            PointF P(float x, float yUp)
            {
                float rx = x * ca + yUp * sa, ry = yUp * ca - x * sa;
                return new PointF(ox + rx * hu, oy - (ry - shift * 1.05f) * hu);
            }

            float ol = Math.Max(1.2f, hu * 0.03f);        // outline thickness
            using var darkPen = new Pen(Outline) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
            using var greenPen = new Pen(Skin) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
            using var darkBrush = new SolidBrush(Outline);
            using var greenBrush = new SolidBrush(Skin);

            // The fingers and thumb, as chains of points (a base, then a joint at the end of each bone).
            var chains = new List<(PointF[] Pts, float Width)>();
            float[] lengths = [0.27f, 0.32f, 0.29f, 0.22f];
            for (int i = 0; i < 4; i++)
            {
                float baseX = -0.09f + i * 0.06f;
                float ang = (i - 1.5f) * 0.2f;            // fingers fan out, so the gaps between them show
                float bx = baseX, by = 0.70f;
                var pts = new List<PointF> { P(bx, by) };
                float[] bone = [0.42f, 0.33f, 0.25f], bend = [0.55f, 0.95f, 0.8f];
                for (int j = 0; j < 3; j++)
                {
                    ang -= curl * bend[j];                // curling bends toward the thumb side (left)
                    bx += MathF.Sin(ang) * lengths[i] * bone[j];
                    by += MathF.Cos(ang) * lengths[i] * bone[j];
                    pts.Add(P(bx, by));
                }
                chains.Add((pts.ToArray(), 0.056f));
            }
            {   // the thumb: starts low on the left of the palm, points up and out, and folds across when curled
                float ang = -0.95f, bx = -0.11f, by = 0.56f;
                var pts = new List<PointF> { P(bx, by) };
                float[] bone = [0.15f, 0.12f], bend = [0.55f, 0.9f];
                for (int j = 0; j < 2; j++)
                {
                    ang += curl * bend[j];
                    bx += MathF.Sin(ang) * bone[j];
                    by += MathF.Cos(ang) * bone[j];
                    pts.Add(P(bx, by));
                }
                chains.Add((pts.ToArray(), 0.08f));
            }

            // The forearm and palm in one lump. It starts well below the ground line so a lean never shows a gap.
            PointF[] arm =
            [
                P(-0.10f, -0.15f), P(0.10f, -0.15f), P(0.085f, 0.38f), P(0.13f, 0.50f), P(0.125f, 0.69f),
                P(0.06f, 0.74f), P(-0.06f, 0.74f), P(-0.125f, 0.69f), P(-0.13f, 0.50f), P(-0.085f, 0.38f),
            ];

            // Pass 1: dark and fat. Pass 2: green and thinner.
            darkPen.Width = ol * 2;
            g.FillPolygon(darkBrush, arm);
            g.DrawPolygon(darkPen, arm);
            foreach (var (pts, fw) in chains) { darkPen.Width = fw * hu + ol * 2; g.DrawLines(darkPen, pts); }

            g.FillPolygon(greenBrush, arm);
            foreach (var (pts, fw) in chains) { greenPen.Width = fw * hu; g.DrawLines(greenPen, pts); }

            // Knuckle bends: a small darker dot at every joint, so it reads as a hand with bones in it.
            using var knuckle = new SolidBrush(Color.FromArgb(150, 82, 118, 78));
            float kr = Math.Max(1f, hu * 0.016f);
            foreach (var (pts, _) in chains)
                for (int j = 1; j < pts.Length - 1; j++)
                    g.FillEllipse(knuckle, pts[j].X - kr, pts[j].Y - kr, kr * 2, kr * 2);

            // The heap of earth at the foot, painted last so it covers the cut edge at the bottom.
            float mw = hu * 0.62f, mh = hu * 0.2f;
            using var soil = new SolidBrush(Color.FromArgb(34, 22, 34));
            using var soilLit = new SolidBrush(Color.FromArgb(70, 50, 52));
            g.FillEllipse(soil, ox - mw / 2, oy - mh / 2 - 1, mw, mh);
            g.FillEllipse(soilLit, ox - mw * 0.3f, oy - mh / 2 - 1, mw * 0.4f, mh * 0.4f);
        });
    }

    public override void Draw(FrameBuffer fb, float t)
    {
        float u = _s.U;
        float whole = Fade(t, Seconds, 0.3f, 0.6f);

        // Which page of which book? Rising 0.4 to 2.0 s, grasping 2.0 to 6.5 s
        // (three loops of 1.5 s), sinking 6.5 to 8.4 s. The rise and sink
        // both ease, so the hand lurches out and slows, like it is heaving itself up.
        Sprite page;
        if (t < 2.0f)
        {
            float p = Smooth((t - 0.4f) / 1.6f);                       // 0 = buried, 1 = out
            page = _rise[(int)MathF.Round((1 - p) * (RisePages - 1))];
        }
        else if (t < 6.5f)
        {
            page = _grasp[(int)((t - 2.0f) / 1.5f % 1f * GraspPages) % GraspPages];
        }
        else
        {
            float p = Smooth((t - 6.5f) / 1.9f);                       // 0 = out, 1 = buried
            page = _rise[(int)MathF.Round(p * (RisePages - 1))];
        }
        // Stamp it with its bottom edge two pixels below the ground line, so no gap shows on a slope.
        page.Draw(fb, (int)MathF.Round(_x - _w / 2f), (int)MathF.Round(_groundY) - _h + 2, whole);

        // The clods: each is thrown up and falls back along a parabola
        // (straight sideways, but upward speed lost steadily to gravity).
        // Position is worked out from the age alone, like everything else here.
        const float gravity = 0.45f;                                   // in U per second squared
        for (int i = 0; i < Clods; i++)
        {
            float age = t - _clodStart[i];
            if (age < 0) continue;
            float up = (_clodVy[i] * age - 0.5f * gravity * age * age) * u;
            if (up < 0) continue;                                      // it has landed: gone into the earth
            float cx = _x + _clodDx[i] * u + _clodVx[i] * u * age;
            (i % 2 == 0 ? _clodBig : _clodSmall).DrawCentered(fb, cx, _groundY - up - 1, whole);
        }
    }
}
