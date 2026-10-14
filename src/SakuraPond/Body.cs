using Peckworks.Screensavers.Core;

namespace SakuraPond;

/// <summary>
/// Draws a long, bendy animal (a koi, a snake) seen from above, along its
/// SPINE: a line of points from the tip of its nose to the end of its tail,
/// each with how wide the body is there.
///
/// FEYNMAN VERSION: think of a fish cut from paper and laid on a bent
/// wire. Wherever the wire bends, the paper body bends with it. So instead
/// of drawing a stiff picture and turning it (which is what made the old
/// trout look like stickers), we bend the wire first and then ask, for
/// every pixel near it, "how far along the fish am I, and how far out to
/// one side?" Those two numbers are called s (0 at the nose, 1 at the base
/// of the tail fin) and v (-1 at the left edge, 0 on the backbone, +1 at
/// the right edge). The animal's own class then says what colour the fish
/// is at that (s, v): a red patch here, a black spot there, the fin's
/// see-through rays at the end. Because the colours are stuck to (s, v), the
/// markings ride along the body as it bends, the way real markings do.
///
/// Finding (s, v) for a pixel: walk the wire one short piece at a time, and
/// for each piece look only at the pixels in its own strip (see Paint).
/// </summary>
internal abstract class Body
{
    /// <summary>The spine, nose first, in screen pixels. Filled by the animal before each draw.</summary>
    protected readonly PointF[] Spine;

    /// <summary>Half the body's width at each spine point, in screen pixels.</summary>
    protected readonly float[] HalfWidth;

    /// <summary>The s (how far along) of each spine point.</summary>
    protected readonly float[] Along;

    protected Body(int points)
    {
        Spine = new PointF[points];
        HalfWidth = new float[points];
        Along = new float[points];
    }

    /// <summary>
    /// The animal's colour at (s, v), plus how solid it is there (0 to 1).
    /// Return false for "not part of me" (a notch in the tail fin, say).
    /// </summary>
    protected abstract bool Colour(float s, float v, out int r, out int g, out int b, out float alpha);

    /// <summary>
    /// Draws the body along the current spine, one piece of spine at a time.
    ///
    /// For each short piece (from one spine point to the next), we visit only
    /// the pixels in its own strip, and inside that strip "how far along" and
    /// "how far out to the side" are just how far the pixel is along the piece
    /// and across it. Neighbouring strips meet on the line that halves the
    /// angle at the joint between their pieces, the way the two sides of a
    /// picture frame meet at a mitred corner, so every pixel of the body
    /// belongs to exactly one strip: no gaps on the outside of a bend, no
    /// pixel painted twice on the inside. (Testing every pixel against every
    /// piece, as a first version did, cost twenty times as much at 4K.)
    /// </summary>
    /// <param name="onlyWhere">Stencil: draw only where true (open water, not under a branch).</param>
    /// <param name="sun">0 to 255 per pixel: how much sun reaches it (branch shadows darken the body too).</param>
    /// <param name="murk">0 = seen clearly, 1 = lost in the water: mixes the body toward the water's colour.</param>
    /// <param name="soft">How many pixels the edge fades over: 1 for a sharp edge, more for a fish deep in the water.</param>
    /// <param name="water">
    /// The painted pond (the backdrop's pixels): what the murk mixes toward.
    /// Null mixes toward whatever is on the frame under the body, which is
    /// right for a thing with nothing between it and the water, and WRONG for
    /// a fish over another fish: the upper one came out two thirds made of the
    /// lower one, and the two looked like they swam through each other. The
    /// haze is the water ABOVE a fish, so it is the water's own colour.
    /// </param>
    /// <param name="ox">Paint the spine moved this far across and down (a shadow thrown to one side), without touching it.</param>
    /// <param name="shadow">0 paints the animal; more paints its shape as a shadow this dark instead (the colours are ignored, the see-through fins stay fainter).</param>
    /// <param name="mark">If given, every pixel painted is set true here (a stencil for what lands ON this body: a shallower fish's shadow).</param>
    /// <param name="clip">If given, nothing outside this screen box is painted.</param>
    protected void Paint(FrameBuffer fb, bool[]? onlyWhere, byte[]? sun, float murk, float soft, uint[]? water = null,
        float ox = 0, float oy = 0, float shadow = 0, bool[]? mark = null, Rectangle? clip = null)
    {
        int n = Spine.Length, w = fb.Width, h = fb.Height;
        uint[] px = fb.Pixels;
        float invSoft = 1 / soft;
        int murk256 = (int)(murk * 256);
        int cx0 = 0, cy0 = 0, cx1 = w - 1, cy1 = h - 1;
        if (clip is Rectangle c)
        {
            cx0 = Math.Max(0, c.Left); cy0 = Math.Max(0, c.Top); cx1 = Math.Min(w - 1, c.Right - 1); cy1 = Math.Min(h - 1, c.Bottom - 1);
            if (cx0 > cx1 || cy0 > cy1) return;
        }
        int sh256 = (int)(shadow * 256);
        Span<float> ux = stackalloc float[n], uy = stackalloc float[n], lens = stackalloc float[n];
        for (int i = 0; i < n - 1; i++)
        {
            float ex = Spine[i + 1].X - Spine[i].X, ey = Spine[i + 1].Y - Spine[i].Y, l = MathF.Sqrt(ex * ex + ey * ey);
            lens[i] = l;
            if (l > 1e-4f) { ux[i] = ex / l; uy[i] = ey / l; }
            else if (i > 0) { ux[i] = ux[i - 1]; uy[i] = uy[i - 1]; }
            else { ux[i] = 1; uy[i] = 0; }
        }

        for (int i = 0; i < n - 1; i++)
        {
            float len = lens[i];
            if (len < 1e-3f) continue;
            PointF a = new(Spine[i].X + ox, Spine[i].Y + oy), b = new(Spine[i + 1].X + ox, Spine[i + 1].Y + oy);
            float dx = ux[i], dy = uy[i], nx = -dy, ny = dx;
            float hwA = HalfWidth[i], hwB = HalfWidth[i + 1];
            float reach = MathF.Max(hwA, hwB) + soft + 1;
            // The mitre lines: at the start joint, halfway between the last
            // piece's direction and this one's; at the end joint, between
            // this one's and the next. The nose and the tail tip have none:
            // there the strip runs on past the end, to round it off.
            bool first = i == 0, last = i == n - 2;
            float sx = first ? dx : dx + ux[i - 1], sy = first ? dy : dy + uy[i - 1];
            float ex2 = last ? dx : dx + ux[i + 1], ey2 = last ? dy : dy + uy[i + 1];
            float before = first ? reach : 0, after = last ? reach : 0;
            float bx0 = MathF.Min(a.X, b.X) - reach - before, bx1 = MathF.Max(a.X, b.X) + reach + after;
            float by0 = MathF.Min(a.Y, b.Y) - reach - before, by1 = MathF.Max(a.Y, b.Y) + reach + after;
            int y0 = Math.Max(cy0, (int)by0), y1 = Math.Min(cy1, (int)by1);
            int xMin = Math.Max(cx0, (int)bx0), xMax = Math.Min(cx1, (int)bx1);
            float sA = Along[i], sB = Along[i + 1];
            for (int y = y0; y <= y1; y++)
            {
                float ry = y + 0.5f - a.Y;
                int row = y * w;
                // Where this strip crosses the row: each limit (out to either
                // side, past the start mitre, before the end mitre) is a
                // straight line, so each gives a stretch of x.
                float lo = xMin - a.X, hi = xMax + 1 - a.X;
                Narrow(ref lo, ref hi, nx, ry * ny, -reach, reach);
                if (first) Narrow(ref lo, ref hi, dx, ry * dy, -before, float.MaxValue);
                else Narrow(ref lo, ref hi, sx, ry * sy, 0, float.MaxValue);
                if (last) Narrow(ref lo, ref hi, dx, ry * dy, float.MinValue, len + after);
                else Narrow(ref lo, ref hi, ex2, ry * ey2 - (len * dx * ex2 + len * dy * ey2), float.MinValue, 0);
                if (lo > hi) continue;
                int xa = Math.Max(xMin, (int)MathF.Floor(a.X + lo - 0.5f)), xb = Math.Min(xMax, (int)MathF.Ceiling(a.X + hi - 0.5f));
                for (int x = xa; x <= xb; x++)
                {
                    int idx = row + x;
                    float rx = x + 0.5f - a.X;
                    // Exactly on which side of each mitre line? (The stretch
                    // above was rounded out to whole pixels, so a pixel right
                    // on a mitre line could otherwise be painted by both
                    // strips, and a fish painted twice over itself shows a
                    // light line across its body.)
                    if (!first && rx * sx + ry * sy < 0) continue;
                    if (!last && (rx - len * dx) * ex2 + (ry - len * dy) * ey2 >= 0) continue;
                    float along = rx * dx + ry * dy;                // pixels along the piece from its start
                    float side = rx * nx + ry * ny;                 // signed: pixels out to one side
                    float t = along <= 0 ? 0 : along >= len ? 1 : along / len;
                    float hw = hwA + (hwB - hwA) * t;
                    // Past the very nose or the tail tip, measure round the end, so the ends are rounded.
                    float overrun = (first && along < 0) ? -along : (last && along > len) ? along - len : 0;
                    float dist = overrun > 0 ? MathF.Sqrt(side * side + overrun * overrun) : MathF.Abs(side);
                    float cover = (hw - dist) * invSoft + 0.5f;
                    if (cover <= 0) continue;
                    if (cover > 1) cover = 1;
                    if (onlyWhere != null && !onlyWhere[idx]) continue;
                    float s = sA + (sB - sA) * t;
                    float v = hw > 0.01f ? Math.Clamp(-side / hw, -1f, 1f) : 0f;
                    if (!Colour(s, v, out int r, out int g, out int bl, out float alpha)) continue;
                    // From here on the arithmetic is whole numbers (solidness
                    // in 256ths, the murk in 256ths): this loop runs tens of
                    // thousands of times a frame per koi, and the float version
                    // with its conversions and clamps cost a third more.
                    int a256 = (int)(alpha * cover * 256);
                    if (sh256 > 0) { r = 10; g = 24; bl = 24; a256 = a256 * sh256 >> 8; }   // the shadow: the body's shape in the water's own dark
                    if (a256 <= 1) continue;
                    if (mark != null) mark[idx] = true;
                    if (sun != null)
                    {
                        int k = sun[idx];
                        r = r * k >> 8; g = g * k >> 8; bl = bl * k >> 8;
                    }
                    uint bg = px[idx];
                    if (murk256 > 0)
                    {
                        // Under water: the deeper, the more the fish takes on the water's own colour.
                        uint wc = water != null ? water[idx] : bg;
                        int wr = (int)((wc >> 16) & 0xFF), wg = (int)((wc >> 8) & 0xFF), wb = (int)(wc & 0xFF);
                        r += (wr - r) * murk256 >> 8; g += (wg - g) * murk256 >> 8; bl += (wb - bl) * murk256 >> 8;
                    }
                    px[idx] = FrameBuffer.Blend256(bg, r, g, bl, a256);
                }
            }
        }
    }

    /// <summary>
    /// The body's shadow: its own shape painted dark and soft, moved dx across
    /// and dy down the screen from where the body is drawn. Used for a koi's
    /// shadow on the bed, and (through CastShadow) for its shadow on the back
    /// of a deeper fish it swims over.
    /// </summary>
    protected void PaintShadow(FrameBuffer fb, bool[]? onlyWhere, float dx, float dy, float alpha, float soft, Rectangle? clip = null)
        => Paint(fb, onlyWhere, null, 0, soft, null, dx, dy, alpha, null, clip);

    /// <summary>
    /// This animal's shadow thrown onto something a "gap" below it in the water
    /// (gap in screen pixels of depth): a deeper fish's back. "onto" is a
    /// stencil of that thing's painted pixels and "clip" its screen box, so
    /// the shadow lands on it and nowhere else. The sun is up and to the left,
    /// so the shadow falls down and right of the caster; under water the sun's
    /// rays bend steeper (about two thirds of the slant they have in air), so
    /// it falls less far per unit of depth than a petal's would; and the deeper
    /// thing's picture sits lower on the screen by the gap (Light.Sink), so the
    /// shadow goes down by that too. Then the whole offset is held to "maxOff":
    /// a shadow thrown further than that from the fish that casts it lands on a
    /// fish nothing is passing over, and reads as a dark blotch, not a shadow
    /// (measured: thrown the full way, a crossing of fish far apart in depth
    /// never got its shadow at all, and one frame in eight had a blotch
    /// somewhere). So the shadow is a soft dark echo just down and right of the
    /// caster, softer the bigger the gap. What says "above" is mostly the deeper
    /// fish being smaller, hazier, softer and dimmer (Koi.Draw); this echo is
    /// the touch that ties the two together where they overlap. Safe to call
    /// for one animal from several threads at once: nothing of its own changes.
    /// </summary>
    public void CastShadow(FrameBuffer fb, bool[] onto, Rectangle clip, float gap, float alpha, float maxOff)
    {
        float dx = Light.ShadowX * 0.67f * gap, dy = (Light.ShadowY * 0.67f + Light.Sink) * gap;
        float len = MathF.Sqrt(dx * dx + dy * dy);
        if (len > maxOff) { dx *= maxOff / len; dy *= maxOff / len; }
        PaintShadow(fb, onto, dx, dy, alpha, MathF.Min(MathF.Max(1f, gap * 0.15f), maxOff * 0.5f), clip);
    }

    /// <summary>
    /// Narrows the stretch [lo, hi] of x-offsets to those where
    /// "x * slope + offset" stays between min and max.
    /// </summary>
    private static void Narrow(ref float lo, ref float hi, float slope, float offset, float min, float max)
    {
        if (MathF.Abs(slope) < 1e-6f)
        {
            if (offset < min || offset > max) { lo = 1; hi = 0; }   // never inside on this row
            return;
        }
        float p = min == float.MinValue ? (slope > 0 ? float.MinValue : float.MaxValue) : (min - offset) / slope;
        float q = max == float.MaxValue ? (slope > 0 ? float.MaxValue : float.MinValue) : (max - offset) / slope;
        if (p > q) (p, q) = (q, p);
        lo = MathF.Max(lo, p);
        hi = MathF.Min(hi, q);
    }

    /// <summary>
    /// A small see-through oval (a koi's side fin), centred at c, pointing
    /// along "angle", length and width in pixels. Simple enough not to need
    /// the spine: turn each pixel into the oval's own frame and test.
    /// </summary>
    /// <param name="rays">Faint rays across a long oval (a fin). False for a solid limb.</param>
    /// <param name="mark">If given, set true at every pixel painted (so a fin counts as part of the body for the shadows that land on it).</param>
    protected static void Oval(FrameBuffer fb, bool[]? onlyWhere, PointF c, float angle, float len, float wid,
        int r, int g, int b, float alpha, float murk, byte[]? sun, bool rays = true, uint[]? water = null, bool[]? mark = null)
    {
        float ca = MathF.Cos(angle), sa = MathF.Sin(angle);
        int reach = (int)MathF.Ceiling(MathF.Max(len, wid)) + 2;
        int x0 = Math.Max(0, (int)c.X - reach), x1 = Math.Min(fb.Width - 1, (int)c.X + reach);
        int y0 = Math.Max(0, (int)c.Y - reach), y1 = Math.Min(fb.Height - 1, (int)c.Y + reach);
        uint[] px = fb.Pixels;
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                int idx = y * fb.Width + x;
                if (onlyWhere != null && !onlyWhere[idx]) continue;
                float dx = x + 0.5f - c.X, dy = y + 0.5f - c.Y;
                float u = (dx * ca + dy * sa) / len, v = (-dx * sa + dy * ca) / wid;
                float q = u * u + v * v;
                if (q >= 1) continue;
                float a = alpha * Math.Clamp((1 - q) * 3, 0, 1) * (rays && len > wid * 1.2f ? 0.75f + 0.25f * MathF.Sin(v * 9) : 1f);   // soft edge; fins get faint rays
                if (mark != null && a > 0.02f) mark[idx] = true;
                int rr = r, gg = g, bb = b;
                if (sun != null) { int k = sun[idx]; rr = rr * k >> 8; gg = gg * k >> 8; bb = bb * k >> 8; }
                uint bg = px[idx];
                if (murk > 0)
                {
                    uint wc = water != null ? water[idx] : bg;      // toward the water's colour, not whatever is under it (see Paint)
                    rr += (int)((((wc >> 16) & 0xFF) - rr) * murk); gg += (int)((((wc >> 8) & 0xFF) - gg) * murk); bb += (int)(((wc & 0xFF) - bb) * murk);
                }
                px[idx] = FrameBuffer.Blend(bg, rr, gg, bb, a);
            }
    }
}
