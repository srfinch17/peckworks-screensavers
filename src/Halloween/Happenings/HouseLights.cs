using System.Drawing.Drawing2D;
using Peckworks.Screensavers.Core;
using Peckworks.Screensavers.Core.Sakura;

namespace Halloween.Happenings;

/// <summary>
/// The haunted house's windows flicker like a failing bulb and go dark one by
/// one. The house sits dark for a moment. Then the windows come back one by
/// one in an eerie green, glow unsteadily, and settle back to their normal
/// yellow.
///
/// How it works: the backdrop already shows the house with four yellow
/// windows. For each window we paint three small round sprites:
///   - DARK: a patch of bare wall (hides the window and its yellow glow).
///   - GREEN: the green pane and its glow, and nothing else (no wall).
///   - YELLOW: the yellow pane and its glow, the same as the backdrop's.
/// Every frame we work out "how yellow" and "how green" each window is
/// right now. Then, in two passes: first ALL FOUR windows are wiped to dark
/// wall, then each window's light is laid back on top at its strength.
/// Think of switching every lamp off and then turning each one up on its
/// own dimmer.
///
/// Why two passes, and why the lights carry no wall: the windows stand so
/// close that their glows overlap. If each window were wiped and relit in
/// turn, the wall in one window's patch would take a round bite out of its
/// neighbor's glow. Wiping everything first, then adding lights that are
/// glow only, lets the glows overlap exactly as they do in the backdrop.
///
/// What color is the dark wall? The house is not pure black in the backdrop:
/// a band of mist was painted over its foot afterwards, so the wall gets a
/// little paler lower down. We cannot ask for "the picture without the
/// glow", so we work the wall color out from the same recipe (see WallAt),
/// at the top and the bottom of the patch, and paint the patch as a smooth
/// blend between the two.
///
/// Every patch is trimmed to stay inside the house's walls (1.5 pixels in),
/// so we never stamp over the house's soft outer edge, which would thicken
/// it. And the patches sit on exactly the backdrop's pixels (rule 11).
///
/// The stencil: the usual "nothing is in front of the house" stencil
/// (OpenFromHouse) is also false wherever the mist lies over the house, so
/// using it as is would cut ragged holes in a patch. So the patches use that
/// stencil PLUS "pixels inside a patch that still look like the wall" (the
/// nearer hill, which can cover the foot of the house, is bluer than the wall
/// and stays in front). A near-black branch in front of a window would look
/// like wall and be painted over. No branch reaches the windows in this
/// scene, so we accept that (and say so here).
/// </summary>
internal sealed class HouseLights : Happening
{
    private static readonly Color Green = Color.FromArgb(120, 255, 120);
    private static readonly Color Yellow = Color.FromArgb(255, 206, 96);          // the same yellow PaintHouse uses

    private readonly HalloweenScenery _s;
    private readonly Sprite[] _dark, _green, _yellow;
    private readonly (int Left, int Top)[] _where;
    private readonly bool[] _stencil;                        // see the note above
    private readonly float[] _goesDark = new float[4];       // when each window gives out
    private readonly float[] _comesGreen = new float[4];     // when each window comes back green
    private readonly float[] _turnsYellow = new float[4];    // when each window changes back to yellow

    public override float Seconds => 12f;
    public override string? Claims => "house";

    public HouseLights(HalloweenScenery s)
    {
        _s = s;
        float u = s.U;
        int n = s.HouseWindows.Length;
        _dark = new Sprite[n]; _green = new Sprite[n]; _yellow = new Sprite[n];
        _where = new (int, int)[n];
        _stencil = (bool[])s.OpenFromHouse.Clone();
        float patchR = u * 0.0235f;                          // just past the window's own glow (0.022 u)
        float inset = 1.5f;                                  // stay this far inside the walls, in pixels

        // The house's outline measurements, the same ones PaintHouse uses.
        float bodyW = u * 0.10f, bodyH = u * 0.065f, bodyTop = s.House.Y - bodyH, hx = s.House.X;
        float towerW = u * 0.034f, towerX = hx - bodyW * 0.5f;

        for (int i = 0; i < n; i++)
        {
            int which = i;
            RectangleF win = s.HouseWindows[i];
            float cx = win.X + win.Width / 2, cy = win.Y + win.Height / 2;

            // Whole-number left and top first (rule 11), then paint at the same fractional offset.
            int left = (int)MathF.Floor(cx - patchR) - 2, top = (int)MathF.Floor(cy - patchR) - 2;
            int size = (int)MathF.Ceiling(patchR * 2) + 6;
            _where[i] = (left, top);

            // The wall color just above and just below the patch (see WallAt).
            Color above = WallAt(s, cy - patchR - 1), below = WallAt(s, cy + patchR + 1);

            Sprite Make(Color? lit) => Sprite.Paint(size, size, g =>
            {
                // The shape the patch may cover: round, and inside the walls.
                using var round = new GraphicsPath();
                round.AddEllipse(cx - left - patchR, cy - top - patchR, patchR * 2, patchR * 2);
                // The walls: the tower (a narrow upright rectangle) AND the
                // main house (its walls and roof as one outline), joined into
                // one shape. Both, for every window, because a glow does not
                // stop at the join: the tower window's glow reaches onto the
                // roof beside it. (A patch cut to the tower alone left that
                // bit of yellow glowing on the roof while the tower was dark.)
                using var walls = new Region(new RectangleF(towerX - towerW / 2 + inset - left, 0, towerW - 2 * inset, size));
                using (var house = new GraphicsPath())
                {
                    float l = hx - bodyW / 2 + inset - left, r = hx + bodyW / 2 - inset - left, t = bodyTop - top;
                    house.AddPolygon([
                        new PointF(l, t + bodyH), new PointF(l, t), new PointF(hx - bodyW * 0.6f + 2 * inset - left, t),
                        new PointF(hx - left, t - u * 0.05f + 2.5f * inset), new PointF(hx + bodyW * 0.6f - 2 * inset - left, t),
                        new PointF(r, t), new PointF(r, t + bodyH)]);
                    walls.Union(house);
                }
                g.SetClip(round);
                g.SetClip(walls, CombineMode.Intersect);

                if (lit is not Color color)
                {
                    // Dark: just the wall, a smooth top-to-bottom blend of the two measured colors.
                    using var wall = new LinearGradientBrush(new PointF(0, cy - top - patchR - 1), new PointF(0, cy - top + patchR + 1), above, below);
                    g.FillRectangle(wall, 0, 0, size, size);
                    return;
                }

                // Lit: the pane and its glow ONLY, on clear glass, with the
                // same colors PaintHouse uses. One thing PaintHouse does not
                // have to think about: the mist is painted over the house
                // afterwards, so a window low enough to stand in the mist is
                // a little paled by it. We pale ours by the same amount
                // (thinner glow, pane mixed toward the mist color), or the
                // window would jump brighter when the happening takes it over.
                float haze = MistAt(s, cy);
                bool usual = color.ToArgb() == Yellow.ToArgb();
                Color glowColor = usual ? Color.FromArgb(255, 190, 70) : Brushwork.Mix(color, Color.FromArgb(255, 170, 40), 0.35f);
                Color rimColor = usual ? Color.FromArgb(255, 170, 50) : color;
                float glowR = u * 0.022f, wx = cx - left, wy = cy - top;
                using var glowPath = new GraphicsPath();
                glowPath.AddEllipse(wx - glowR, wy - glowR, glowR * 2, glowR * 2);
                using var glow = new PathGradientBrush(glowPath)
                {
                    CenterColor = Color.FromArgb((int)(110 * (1 - haze)), Brushwork.Mix(glowColor, Mist, haze)),
                    SurroundColors = [Color.FromArgb(0, rimColor)],
                };
                g.FillPath(glow, glowPath);
                using var pane = new SolidBrush(Brushwork.Mix(color, Mist, haze));
                g.FillRectangle(pane, new RectangleF(win.X - left, win.Y - top, win.Width, win.Height));
            });
            _dark[i] = Make(null);
            _green[i] = Make(Green);
            _yellow[i] = Make(Yellow);

            // Open this patch in the stencil (see the class note): a pixel counts as
            // "still the house" if it is the wall color we expect (plus, near the
            // window, some warm glow). The nearer hill is a little BLUER than
            // the wall, so a bluer pixel is the hill standing in front: leave it.
            for (int y = Math.Max(0, top); y < Math.Min(s.Height, top + size); y++)
                for (int x = Math.Max(0, left); x < Math.Min(s.Width, left + size); x++)
                {
                    if (_stencil[y * s.Width + x]) continue;
                    uint p = s.Pixels[y * s.Width + x];
                    Color e = WallAt(s, y);
                    int dr = (int)((p >> 16) & 0xFF) - e.R, dg = (int)((p >> 8) & 0xFF) - e.G, db = (int)(p & 0xFF) - e.B;
                    if (db >= -6 && db <= 11 && dr >= -6 && dg >= -6) _stencil[y * s.Width + x] = true;
                }
        }
    }

    /// <summary>
    /// What the backdrop's bare wall looks like at height y: the house's ink
    /// with the mist band painted over it afterwards. This repeats the mist
    /// recipe from HalloweenPainter.PaintMist (a band from 0.07 u above the
    /// horizon to 0.08 u below, pale at 120/255 in the middle, fading to
    /// nothing at both edges), so the dark patch is the very same color as
    /// the wall around it.
    /// </summary>
    private static Color WallAt(HalloweenScenery s, float y)
    {
        float a = MistAt(s, y);
        Color ink = Color.FromArgb(12, 8, 20);
        return Color.FromArgb((int)(ink.R + (Mist.R - ink.R) * a), (int)(ink.G + (Mist.G - ink.G) * a), (int)(ink.B + (Mist.B - ink.B) * a));
    }

    private static readonly Color Mist = Color.FromArgb(170, 150, 190);           // the mist's color, as in PaintMist

    /// <summary>How thick the mist is at height y: 0 = none, up to about 0.47 in the middle of its band.</summary>
    private static float MistAt(HalloweenScenery s, float y)
    {
        float top = s.Horizon - s.U * 0.07f - 1, bottom = s.Horizon + s.U * 0.08f + 1;
        float pos = Math.Clamp((y - top) / (bottom - top), 0f, 1f);
        return (pos < 0.5f ? pos : 1 - pos) * 2 * 120f / 255f;
    }

    public override void Begin(Random rng)
    {
        // Each window gets its own slot in a random order, so the sequence is
        // different every time.
        int[] order = [0, 1, 2, 3];
        for (int i = order.Length - 1; i > 0; i--) { int j = rng.Next(i + 1); (order[i], order[j]) = (order[j], order[i]); }
        for (int k = 0; k < 4; k++)
            _goesDark[order[k]] = 1.0f + 0.5f * k + 0.2f * (float)rng.NextDouble();   // last one is out by about 2.7 s

        int[] back = [0, 1, 2, 3];
        for (int i = back.Length - 1; i > 0; i--) { int j = rng.Next(i + 1); (back[i], back[j]) = (back[j], back[i]); }
        for (int k = 0; k < 4; k++)
        {
            _comesGreen[back[k]] = 4.3f + 0.5f * k + 0.15f * (float)rng.NextDouble();
            _turnsYellow[back[k]] = 8.0f + 0.4f * k + 0.15f * (float)rng.NextDouble();
        }
    }

    /// <summary>A repeatable "random" number from 0 to 1 for a whole number: the same input always gives the same answer, so Draw needs no memory.</summary>
    private static float Hash(int n)
    {
        float v = MathF.Sin(n * 12.9898f) * 43758.5453f;
        return v - MathF.Floor(v);
    }

    public override void Draw(FrameBuffer fb, float t)
    {
        // ---- how yellow and how green is each window right now? ----
        // (stackalloc: a tiny scratch list that costs nothing to make each frame)
        Span<float> yellows = stackalloc float[_dark.Length], greens = stackalloc float[_dark.Length];
        bool allAsPainted = true;
        for (int i = 0; i < _dark.Length; i++)
        {
            float yellow, green = 0;
            if (t < _goesDark[i])
            {
                // Before it gives out: steady yellow, then more and more
                // flicker (a bulb dying) in its last second.
                float flickerAmount = Smooth(1 - (_goesDark[i] - t) / 1.0f);
                bool dropped = Hash((int)(t * 13) * 7 + i) < 0.55f * flickerAmount;
                yellow = dropped ? 0.2f : 1f;
            }
            else if (t < _comesGreen[i]) yellow = 0;                           // dark
            else
            {
                // Green: swells in over half a second, then wobbles: two sine
                // waves of different speeds, and now and then a stutter.
                float swell = Smooth((t - _comesGreen[i]) / 0.5f);
                float wobble = 0.78f + 0.14f * MathF.Sin(t * 7f + i * 2f) + 0.08f * MathF.Sin(t * 19f + i);
                if (Hash((int)(t * 11) * 5 + i * 3) < 0.12f) wobble *= 0.45f;
                float toYellow = Smooth((t - _turnsYellow[i]) / 0.9f);        // later the green melts into yellow
                green = swell * wobble * (1 - toYellow);
                yellow = toYellow;
            }

            yellows[i] = yellow;
            greens[i] = green;
            if (yellow < 0.999f || green > 0) allAsPainted = false;
        }

        // Every window plain yellow is exactly the backdrop: stamp nothing, so the start and the end are pixel-perfect.
        if (allAsPainted) return;

        // Pass 1: every window to dark wall, even the ones still lit.
        for (int i = 0; i < _dark.Length; i++)
            _dark[i].Draw(fb, _where[i].Left, _where[i].Top, 1f, _stencil);

        // Pass 2: each window's light back on top (see the class note for why it is two passes).
        for (int i = 0; i < _dark.Length; i++)
        {
            if (greens[i] > 0) _green[i].Draw(fb, _where[i].Left, _where[i].Top, greens[i], _stencil);
            if (yellows[i] > 0) _yellow[i].Draw(fb, _where[i].Left, _where[i].Top, yellows[i], _stencil);
        }
    }
}
