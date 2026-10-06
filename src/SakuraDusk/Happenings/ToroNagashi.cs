using System.Drawing.Drawing2D;
using Peckworks.Screensavers.Core;

namespace SakuraDusk.Happenings;

/// <summary>
/// Toro nagashi: floating paper lanterns. In Japan, on summer evenings, people
/// set little paper lanterns on a river with a candle inside, and let them
/// drift away. Each one carries a wish or a remembered name. Here, six to nine
/// of them float slowly across the pond in a loose line, each with a warm gold
/// reflection wobbling below it.
///
/// FEYNMAN VERSION: a lantern is a tiny glowing box (paper sides lit from
/// inside by a candle) standing on a wooden float. We paint each one ONCE as a
/// sticker (a Sprite), in four sizes (far ones are small, near ones bigger) and
/// three paper colors. Every frame we only stamp the right sticker at the right
/// spot, then add three bits of light that change from frame to frame: a soft
/// halo, a tiny hot flame core (both flicker), and the long reflection, which
/// is drawn as a stack of thin horizontal slivers that wiggle sideways, like
/// light on gently moving water.
///
/// Everything goes through the OpenWater stencil, so the lanterns slip behind
/// the bridge posts and the banks instead of floating over them.
/// </summary>
internal sealed class ToroNagashi : Happening
{
    private const int Tiers = 4, Kinds = 3;

    // Paper colors: (middle, edge) of the lit paper, and the reflection's color.
    private static readonly (Color Mid, Color Edge, Color Mirror)[] Paper =
    [
        (Color.FromArgb(255, 248, 214), Color.FromArgb(240, 180, 100), Color.FromArgb(255, 208, 128)),   // warm white-gold
        (Color.FromArgb(255, 232, 232), Color.FromArgb(238, 154, 176), Color.FromArgb(255, 176, 176)),   // pale pink
        (Color.FromArgb(255, 206, 178), Color.FromArgb(216, 100, 88), Color.FromArgb(255, 152, 120)),    // pale red
    ];

    private readonly DuskScenery _s;
    private readonly float _u;
    private readonly float _near, _far;                // the nearest and farthest water a lantern floats on
    private readonly int[] _side = new int[Tiers];     // paper side in pixels, per size tier
    private readonly Sprite[,] _lantern = new Sprite[Tiers, Kinds];
    private readonly Sprite[] _halo = new Sprite[Tiers], _core = new Sprite[Tiers];

    // One lantern's story for this showing, rolled in Begin.
    private struct Lamp { public float X0, Depth, Speed, Phase, Phase2; public int Kind; }
    private Lamp[] _lamps = [];
    private int _dir = 1;

    public override float Seconds => 20f;
    public override string? Claims => "pond";
    public override int Layer => 1;

    public ToroNagashi(DuskScenery s)
    {
        _s = s;
        _u = s.U;
        _far = s.Horizon + _u * 0.06f;
        _near = MathF.Max(_far + _u * 0.02f, s.Bridge.WaterLine - _u * 0.01f);

        for (int k = 0; k < Tiers; k++)
        {
            // Far (tier 0) 0.014 U wide up to near (tier 3) 0.025 U, never under 5 pixels.
            int side = _side[k] = Math.Max(5, (int)(_u * (0.014f + 0.011f * k / (Tiers - 1))));
            for (int c = 0; c < Kinds; c++) _lantern[k, c] = PaintLantern(side, Paper[c].Mid, Paper[c].Edge);
            _halo[k] = Sprite.Glow((int)(side * 1.9f), Color.FromArgb(255, 190, 110));
            _core[k] = Sprite.Glow(Math.Max(2, (int)(side * 0.55f)), Color.FromArgb(255, 236, 170));
        }
    }

    /// <summary>
    /// One lantern, drawn standing on its base (the base's bottom is the
    /// sprite's bottom row). A dark wooden float, a cube of paper that is
    /// brightest in the middle (the candle) and deeper gold toward the edges,
    /// a darker frame round it, and a thin wooden lid.
    /// </summary>
    private static Sprite PaintLantern(int side, Color mid, Color edge)
    {
        int w = side * 3 / 2 + 4, h = side * 3 / 2 + 4;
        float baseH = MathF.Max(2f, side * 0.2f), paperH = side * 0.95f, lid = MathF.Max(1.5f, side * 0.1f);
        return Sprite.Paint(w, h, g =>
        {
            float cx = w / 2f, bottom = h - 2f;
            using var wood = new SolidBrush(Color.FromArgb(104, 60, 46));
            g.FillRectangle(wood, cx - side * 0.55f, bottom - baseH, side * 1.1f, baseH);

            float top = bottom - baseH - paperH;
            var paper = new RectangleF(cx - side / 2f, top, side, paperH);
            using (var path = new GraphicsPath())
            {
                path.AddRectangle(paper);
                using var lit = new PathGradientBrush(path) { CenterColor = mid, SurroundColors = [edge] };
                g.FillPath(lit, path);
            }
            // The frame: slightly darker, so the paper reads as a box, not a blob.
            using var frame = new Pen(Color.FromArgb(200, 128, 72, 46), MathF.Max(1f, side * 0.07f));
            g.DrawRectangle(frame, paper.X, paper.Y, paper.Width, paper.Height);
            using var rib = new Pen(Color.FromArgb(70, 128, 72, 46), 1f);
            g.DrawLine(rib, cx, top, cx, top + paperH);
            g.FillRectangle(wood, cx - side * 0.56f, top - lid, side * 1.12f, lid);
        });
    }

    public override void Begin(Random rng)
    {
        _dir = rng.Next(2) == 0 ? 1 : -1;
        int n = 6 + rng.Next(4);                                   // 6 to 9
        _lamps = new Lamp[n];
        float span = _s.Width * 0.62f;
        float travel = 0.03f * _u * Seconds;
        for (int i = 0; i < n; i++)
        {
            // A loose line: spread evenly along it (with wobble), sloping a
            // little so one end is nearer to us than the other.
            float along = (i + 0.2f + 0.6f * (float)rng.NextDouble()) / n - 0.5f;          // -0.5 to 0.5
            float depth = Math.Clamp(0.5f + _dir * along * 0.5f + ((float)rng.NextDouble() - 0.5f) * 0.45f, 0f, 1f);
            _lamps[i] = new Lamp
            {
                // Centered so the line is mid-pond halfway through the showing.
                X0 = _s.Width * 0.5f + along * span - _dir * travel * 0.5f,
                Depth = depth,
                Speed = 0.03f * _u * (0.88f + 0.24f * (float)rng.NextDouble()),
                Phase = (float)rng.NextDouble() * MathF.Tau,
                Phase2 = (float)rng.NextDouble() * MathF.Tau,
                Kind = rng.NextDouble() < 0.28 ? 1 + rng.Next(2) : 0,   // mostly gold, some pink or red
            };
        }
        Array.Sort(_lamps, (a, b) => a.Depth.CompareTo(b.Depth));        // far ones first, near ones paint over them
    }

    public override void Draw(FrameBuffer fb, float t)
    {
        float on = Fade(t, Seconds, 2.5f, 3f);
        foreach (var l in _lamps)
        {
            int tier = Math.Clamp((int)MathF.Round(l.Depth * (Tiers - 1)), 0, Tiers - 1);
            int side = _side[tier];

            float x = l.X0 + _dir * l.Speed * t;
            float bobAmt = MathF.Max(1f, side * 0.07f);
            // Rounded once: the lantern and its reflection move by the same whole pixels.
            float bob = MathF.Round(bobAmt * (float)Math.Sin(t * 1.25 + l.Phase));
            float y = MathF.Round(_far + l.Depth * (_near - _far) + bob);
            if (x < -side * 3 || x > _s.Width + side * 3) continue;

            float flick = 0.82f + 0.12f * (float)Math.Sin(t * 7.3 + l.Phase) + 0.06f * (float)Math.Sin(t * 13.1 + l.Phase2);

            // The reflection first, so the lantern stands on top of it.
            DrawReflection(fb, x, y, side, l, t, on * flick);

            Sprite lantern = _lantern[tier, l.Kind];
            int left = (int)MathF.Round(x - lantern.Width / 2f), top = (int)y - lantern.Height + 2;
            float paperY = top + lantern.Height - 2 - MathF.Max(2f, side * 0.2f) - side * 0.475f;   // the middle of the paper
            _halo[tier].DrawCentered(fb, x, paperY, 0.55f * on * flick, _s.OpenWater);
            lantern.Draw(fb, left, top, on, _s.OpenWater);
            _core[tier].DrawCentered(fb, x, paperY, 0.75f * on * flick, _s.OpenWater);
        }
    }

    /// <summary>
    /// The long warm streak under a lantern: thin horizontal slivers stacked
    /// downward. Each sliver is a little narrower and fainter than the one
    /// above, and wiggles left and right on its own wave, with a ripple that
    /// switches the brightness up and down, so the streak looks broken by the
    /// water instead of being a straight bar.
    /// </summary>
    private void DrawReflection(FrameBuffer fb, float x, float y, int side, Lamp l, float t, float strength)
    {
        Color mirror = Paper[l.Kind].Mirror;
        float len = side * (3.2f + 1.6f * l.Depth);
        // Thin slivers: the smooth wiggle is the whole point, so no coarse steps.
        float step = MathF.Max(1f, side / 24f);                               // 1 pixel on a small screen, 2 on 4K: the cost stays flat
        for (float k = step; k < len; k += step)
        {
            float f = k / len;                                                    // 0 at the lantern, 1 at the streak's tip
            float kk = k / side;                                                  // distance in "lantern widths", so every size wiggles alike
            float wob = (float)Math.Sin(kk * 2.6 + t * 1.7 + l.Phase) * side * 0.22f * (0.2f + f);
            float ripple = 0.7f + 0.3f * (float)Math.Sin(kk * 6.0 - t * 2.4 + l.Phase2);
            float alpha = 0.62f * MathF.Pow(1f - f, 1.3f) * ripple * strength;
            float half = side * 0.5f * (1f - 0.4f * f);
            fb.Line(new PointF(x - half + wob, y + k), new PointF(x + half + wob, y + k), mirror, alpha, step * 1.1f, _s.OpenWater);
        }
    }
}
