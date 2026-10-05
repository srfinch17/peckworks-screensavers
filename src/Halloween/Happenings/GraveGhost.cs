using System.Drawing.Drawing2D;
using Peckworks.Screensavers.Core;

namespace Halloween.Happenings;

/// <summary>
/// A ghost seeps up out of the ground at one tombstone, rises to hover just
/// above it, wobbles, says "BOO!" (arms up, mouth wide), then drifts upward
/// and fades away.
///
/// The ghost is a classic bedsheet ghost: a round head, a body that narrows
/// a little toward the bottom, and a wavy hem. Two flip-books are painted
/// once in the constructor: 16 calm poses and 16 "boo" poses. Within each
/// book the only thing that changes from page to page is the ripple of the
/// hem, like a flag moving in a breeze. Page 0 and page 16 are the same
/// picture, so the loop has no seam.
///
/// The "boo" is a crossfade: the calm ghost is always stamped, and the boo
/// ghost is stamped on top of it with an opacity that goes 0, 1, 0. Like two
/// slides in a projector, one dissolving into the other.
///
/// Everything is a function of t: the rise, the wobble, the boo and the
/// exit are all worked out fresh each frame from the seconds since it began.
/// </summary>
internal sealed class GraveGhost : Happening
{
    private const int Poses = 16;

    private readonly HalloweenScenery _s;
    private readonly Sprite[] _calm = new Sprite[Poses];
    private readonly Sprite[] _boo = new Sprite[Poses];
    private readonly Sprite _glow;
    private readonly int _sheetW, _sheetH;       // the size of each ghost sprite
    private PointF _foot;                        // where the chosen stone meets the ground
    private float _stoneTop;                     // the y of the top of that stone

    public override float Seconds => 11f;
    // One happening at a time among the stones and pumpkins: they are drawn
    // in the order they began, with no idea of who stands in front, so a cat
    // and a zombie hand on the same spot would be drawn through each other.
    public override string? Claims => "graveyard";

    public GraveGhost(HalloweenScenery s)
    {
        _s = s;
        float bodyW = Math.Max(10f, s.U * 0.055f);       // never thinner than 10 px, even in the tiny preview box
        float bodyH = Math.Max(14f, s.U * 0.08f);
        _sheetW = (int)(bodyW * 2.1f);                   // wide enough for the arms of the boo pose
        _sheetH = (int)bodyH + 4;
        for (int i = 0; i < Poses; i++)
        {
            _calm[i] = PaintGhost(_sheetW, _sheetH, bodyW, i, boo: false);
            _boo[i] = PaintGhost(_sheetW, _sheetH, bodyW, i, boo: true);
        }
        // A soft pale-blue halo that goes behind the ghost: the glow of something not quite of this world.
        _glow = Sprite.Glow((int)Math.Max(8f, s.U * 0.07f), Color.FromArgb(150, 190, 255));
    }

    public override void Begin(Random rng)
    {
        var stone = _s.Tombstones[rng.Next(_s.Tombstones.Count)];
        _foot = stone.Foot;
        // A cross stands taller than a slab (the painter makes it 1.15 slab-heights).
        _stoneTop = stone.Foot.Y - _s.TombstoneSize.Height * (stone.Cross ? 1.15f : 1f);
    }

    /// <summary>
    /// Paints one page of the flip-book. The ghost is a polygon: the top
    /// half of a circle for the head, down the right side, along a wavy hem
    /// from right to left, and back up the left side. "pose" shifts the
    /// ripple of the hem along; "boo" raises the arms and opens the mouth.
    /// </summary>
    private static Sprite PaintGhost(int sw, int sh, float bodyW, int pose, bool boo)
    {
        return Sprite.Paint(sw, sh, g =>
        {
            float cx = sw / 2f, r = bodyW / 2f, top = 2f;
            float amp = bodyW * 0.07f;                       // how far the hem waves up and down
            float hemY = sh - amp - 2f;
            float phase = pose * MathF.Tau / Poses;
            float line = Math.Max(1f, bodyW * 0.035f);

            // The outline of the body, point by point.
            var pts = new List<PointF>();
            for (int i = 0; i <= 24; i++)                    // the head: left, over the top, to the right
            {
                float a = MathF.PI + MathF.PI * i / 24f;
                pts.Add(new PointF(cx + r * MathF.Cos(a), top + r + r * MathF.Sin(a)));
            }
            float mid = top + r + (hemY - top - r) * 0.5f;
            pts.Add(new PointF(cx + r * 0.97f, mid));        // the right side, narrowing as it goes down
            for (int i = 0; i <= 30; i++)                    // the hem, right to left, rippling like a flag
            {
                float f = i / 30f;
                pts.Add(new PointF(cx + r * 0.86f - 2 * r * 0.86f * f,
                                   hemY + amp * MathF.Sin(phase + f * 3 * MathF.Tau)));
            }
            pts.Add(new PointF(cx - r * 0.97f, mid));        // and back up the left side

            // The arms go in BEHIND the body in the boo pose, so the sleeves look like they come from the sheet.
            if (boo)
            {
                foreach (float side in (float[])[-1f, 1f])
                {
                    // Shoulder, then a bent elbow, then a floppy hand, drawn as a smooth curve so it reads as a sleeve, not a plank.
                    PointF[] arm =
                    [
                        new(cx + side * r * 0.75f, top + r * 1.55f),
                        new(cx + side * r * 1.4f, top + r * 1.2f),
                        new(cx + side * r * 1.55f, top + r * 0.45f),
                    ];
                    using var edge = new Pen(Color.FromArgb(120, 145, 205), r * 0.38f + line * 2) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
                    using var cloth = new Pen(Color.FromArgb(236, 244, 255), r * 0.38f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
                    g.DrawCurve(edge, arm, 0.5f);
                    g.DrawCurve(cloth, arm, 0.5f);
                    // A rounded hand blob at the end of the sleeve.
                    float hr = r * 0.26f;
                    using var hb = new SolidBrush(Color.FromArgb(236, 244, 255));
                    using var hp = new Pen(Color.FromArgb(120, 145, 205), line);
                    g.FillEllipse(hb, arm[2].X - hr, arm[2].Y - hr, hr * 2, hr * 2);
                    g.DrawEllipse(hp, arm[2].X - hr, arm[2].Y - hr, hr * 2, hr * 2);
                }
            }

            // The sheet: lighter at the head, a little bluer toward the hem.
            using (var fill = new LinearGradientBrush(new RectangleF(0, top, sw, sh - top),
                       Color.FromArgb(246, 250, 255), Color.FromArgb(196, 214, 246), 90f))
            using (var edge = new Pen(Color.FromArgb(120, 145, 205), line) { LineJoin = LineJoin.Round })
            {
                g.FillPolygon(fill, pts.ToArray());
                g.DrawPolygon(edge, pts.ToArray());
            }

            // The face: two dark eyes and a mouth. In the boo pose the eyes go wide and the mouth is a big dark O.
            using var dark = new SolidBrush(Color.FromArgb(240, 26, 28, 60));
            float ew = r * (boo ? 0.34f : 0.26f), eh = r * (boo ? 0.52f : 0.4f);
            float eyeY = top + r * 0.95f;
            g.FillEllipse(dark, cx - r * 0.38f - ew / 2, eyeY - eh / 2, ew, eh);
            g.FillEllipse(dark, cx + r * 0.38f - ew / 2, eyeY - eh / 2, ew, eh);
            float mw = r * (boo ? 0.56f : 0.22f), mh = r * (boo ? 0.8f : 0.28f);
            float mouthY = top + r * (boo ? 1.55f : 1.5f);
            g.FillEllipse(dark, cx - mw / 2, mouthY - mh / 2, mw, mh);
        });
    }

    public override void Draw(FrameBuffer fb, float t)
    {
        float u = _s.U;

        // RISE: from the ground (the hem sitting on it) up to hovering 0.09 U
        // above the stone's top. "Ease out": fast at first, slowing to a
        // stop, like a balloon that has found its height. 1 - (1 - x)^3.
        float rise = Math.Clamp(t / 3.2f, 0f, 1f);
        float eased = 1 - (1 - rise) * (1 - rise) * (1 - rise);
        float startY = _foot.Y - _sheetH / 2f;
        float hoverY = _stoneTop - u * 0.09f;
        float y = startY + (hoverY - startY) * eased;

        // BOO: 0 normally, 1 while shouting. Up at 5.0 s, held to about 6.9, back down by 7.4.
        float boo = Smooth((t - 5f) / 0.4f) * Smooth((7.4f - t) / 0.5f);
        y -= u * 0.012f * boo;                               // a little lunge up as it shouts

        // WOBBLE: two slow sine waves of different speeds, side to side and a gentle bob.
        float x = _foot.X + u * 0.012f * MathF.Sin(t * 1.7f) + u * 0.005f * MathF.Sin(t * 3.1f);
        y += Math.Max(1f, u * 0.004f) * MathF.Sin(t * 1.9f);

        // EXIT: from 8.2 s it drifts upward, getting thinner.
        y -= u * 0.12f * Smooth((t - 8.2f) / 2.8f);

        // OPACITY: nearly clear at the ground, filling in to 0.75 as it rises; clear again at the end.
        float opacity = 0.75f * Smooth(t / 2.8f) * Smooth((Seconds - t) / 2.4f);
        int left = (int)MathF.Round(x - _sheetW / 2f), top = (int)MathF.Round(y - _sheetH / 2f);

        _glow.DrawCentered(fb, x, y, Math.Min(1f, opacity * 1.1f));

        int page = (int)(t * 14f) % Poses;                   // 14 pages a second: the hem ripples about once a second
        _calm[page].Draw(fb, left, top, opacity);
        if (boo > 0.01f) _boo[page].Draw(fb, left, top, opacity * boo);
    }
}
