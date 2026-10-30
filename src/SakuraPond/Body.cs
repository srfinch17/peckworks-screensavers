using Peckworks.Screensavers.Core;
using Peckworks.Screensavers.Core.Minds;

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

    /// <summary>
    /// The body's shape for touching and keeping clear (Core/Minds Chain):
    /// one bead per spine point, in GROUND pixels (x, y as if seen straight
    /// down, before the slant and the sink) with z the depth in pixels. The
    /// animal fills it as it lays its spine. The koi ask every other body's
    /// chain how much room there is, and KoiSchool pushes two apart when they
    /// overlap, so no two animals in the water are ever drawn through each
    /// other.
    /// </summary>
    public readonly Chain Shape;

    /// <summary>The spine as drawn this frame, nose first, screen pixels (what shoves the floating petals aside: Petals.PushAside).</summary>
    public ReadOnlySpan<PointF> Drawn => Spine;

    /// <summary>Half the body's width at each drawn spine point, screen pixels.</summary>
    public ReadOnlySpan<float> DrawnHalfWidth => HalfWidth;

    protected Body(int points)
    {
        Spine = new PointF[points];
        HalfWidth = new float[points];
        Along = new float[points];
        Shape = new Chain(points);
    }

    /// <summary>
    /// The animal's colour at (s, v), plus how solid it is there (0 to 1).
    /// Return false for "not part of me" (a notch in the tail fin, say).
    /// </summary>
    protected abstract bool Colour(float s, float v, out int r, out int g, out int b, out float alpha);

    /// <summary>
    /// How solid the body is at (s, v), 0 to 1, for a shadow pass (the colours
    /// are not wanted there). By default it asks Colour and keeps only the
    /// alpha; an animal whose colour is costly to work out (the koi's skin
    /// sheets) answers directly. Must agree with Colour, or the shadow and the
    /// body have different shapes.
    /// </summary>
    protected virtual float ShadowAlpha(float s, float v) => Colour(s, v, out _, out _, out _, out float a) ? a : 0;

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
    /// <param name="murkAt">
    /// If given, the murk at each spine point instead of one for the whole
    /// body (between two points, the slope between theirs): a fish diving
    /// head first is deeper, so hazier, at the head than at the tail.
    /// </param>
    protected void Paint(FrameBuffer fb, bool[]? onlyWhere, byte[]? sun, float murk, float soft, uint[]? water = null,
        float ox = 0, float oy = 0, float shadow = 0, bool[]? mark = null, Rectangle? clip = null, float[]? murkAt = null)
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
            float sA = Along[i], sB = Along[i + 1], invLen = 1 / len;
            int mA = murkAt != null ? (int)(murkAt[i] * 256) : murk256, mB = murkAt != null ? (int)(murkAt[i + 1] * 256) : murk256;
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
                    if (onlyWhere != null && !onlyWhere[idx]) continue;   // the stencil first: a pixel under a branch or a pad costs nothing more than this read (it was tested after the mitres, the distances and a square root)
                    float rx = x + 0.5f - a.X;
                    // Exactly on which side of each mitre line? (The stretch
                    // above was rounded out to whole pixels, so a pixel right
                    // on a mitre line could otherwise be painted by both
                    // strips, and a fish painted twice over itself shows a
                    // light line across its body.) Only the two pixels at
                    // either end of the stretch can be the rounded-out ones,
                    // so only they are asked (four multiplies a pixel saved
                    // over the whole body; proved pixel-identical).
                    if (x <= xa + 1 || x >= xb - 1)
                    {
                        if (!first && rx * sx + ry * sy < 0) continue;
                        if (!last && (rx - len * dx) * ex2 + (ry - len * dy) * ey2 >= 0) continue;
                    }
                    float along = rx * dx + ry * dy;                // pixels along the piece from its start
                    float side = rx * nx + ry * ny;                 // signed: pixels out to one side
                    float t = along <= 0 ? 0 : along >= len ? 1 : along * invLen;
                    float hw = hwA + (hwB - hwA) * t;
                    // Past the very nose or the tail tip, measure round the end, so the ends are rounded.
                    float overrun = (first && along < 0) ? -along : (last && along > len) ? along - len : 0;
                    float dist = overrun > 0 ? MathF.Sqrt(side * side + overrun * overrun) : MathF.Abs(side);
                    float cover = (hw - dist) * invSoft + 0.5f;
                    if (cover <= 0) continue;
                    if (cover > 1) cover = 1;
                    float s = sA + (sB - sA) * t;
                    float v = hw > 0.01f ? Math.Clamp(-side / hw, -1f, 1f) : 0f;
                    int r = 0, g = 0, bl = 0; float alpha;
                    // A shadow pass needs only how solid the body is here, never its colours: the koi's
                    // bed shadow and every cross shadow are whole-body passes, and they were asking the
                    // skin for colours they threw away (about half of a koi's pixel work was shadows).
                    if (sh256 > 0) { alpha = ShadowAlpha(s, v); if (alpha <= 0) continue; }
                    else if (!Colour(s, v, out r, out g, out bl, out alpha)) continue;
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
                    int m256 = mA + ((mB - mA) * (int)(t * 256) >> 8);     // this piece's murk: the same at both ends, or the slope between them
                    if (m256 > 0)
                    {
                        Deepen(ref r, ref g, ref bl, m256);                 // darker and greyer by its depth first (its own colour fades)...
                        // ...then under water: the deeper, the more the fish takes on the water's own colour.
                        uint wc = water != null ? water[idx] : bg;
                        int wr = (int)((wc >> 16) & 0xFF), wg = (int)((wc >> 8) & 0xFF), wb = (int)(wc & 0xFF);
                        r += (wr - r) * m256 >> 8; g += (wg - g) * m256 >> 8; bl += (wb - bl) * m256 >> 8;
                    }
                    px[idx] = FrameBuffer.Blend256(bg, r, g, bl, a256);
                }
            }
        }
    }

    /// <summary>
    /// What depth does to a thing's OWN colour, before the water's colour is
    /// mixed over it: darker and greyer the deeper it is. Water eats light,
    /// the warm end first, so a red koi a hand down is a dull red and one on
    /// the bed is a brownish grey; the murk mix alone (toward the water's
    /// teal) kept the fish's own share as bright as at the surface, and the
    /// owner asked for depth to show in the colour ("when a creature swims
    /// deeper it gets darker, less color vibrant"). The dial is the murk in
    /// 256ths (0.14 at the surface, 1 at the limit of sight, Light.Murk):
    /// nothing happens at the surface, and at the limit the colour is left
    /// with four tenths of its saturation and six tenths of its brightness.
    /// Whole numbers only: this runs once per painted pixel.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    internal static void Deepen(ref int r, ref int g, ref int b, int murk256)      // (internal since round forty-one: the submerged stones share the rule, PondPainter.WrapIsland)
    {
        // ROUND FORTY ("animals lose a little color when they go deeper because not all that light can reflect back
        // out of the water"): the veil (Light.Murk) is thinner now and tops out at 0.62, so this is where depth shows.
        // The dial runs 0 at the surface to about 100 on the deepest bed (0.42 - 0.12 in 256ths; held at 110). On the
        // typical bed (dial 66) a thing keeps six tenths of its saturation, and its brightness goes to half in the red,
        // two thirds in the green and seven tenths in the blue: water eats the warm end of the light first, so a red koi
        // down there is a dull brown-red and a white one a pale blue-grey, both plainly fish and plainly deep.
        int t = murk256 - 31;                                   // 0.12 * 256: the surface's own haze is not depth
        if (t <= 0) return;
        if (t > 110) t = 110;
        int lum = (r * 77 + g * 150 + b * 29) >> 8;             // the pixel's brightness, the eye's weights
        int sat = 256 - t * 3 / 2;                              // (until 2026-10-29: 256 - t * 150 / 220 and one darkening for all three channels, 256 - t * 140 / 220, with t up to 220)
        int darkR = 256 - t * 2, darkG = 256 - t * 7 / 5, darkB = 256 - t * 6 / 5;
        r = (lum + ((r - lum) * sat >> 8)) * darkR >> 8;
        g = (lum + ((g - lum) * sat >> 8)) * darkG >> 8;
        b = (lum + ((b - lum) * sat >> 8)) * darkB >> 8;
    }

    /// <summary>
    /// The body's shadow: its own shape painted dark and soft, moved dx across
    /// and dy down the screen from where the body is drawn. Used for a koi's
    /// shadow on the bed, and (through CastShadow) for its shadow on the back
    /// of a deeper fish it swims over.
    /// </summary>
    protected void PaintShadow(FrameBuffer fb, bool[]? onlyWhere, float dx, float dy, float alpha, float soft, Rectangle? clip = null)
    {
        // Paint tells a shadow from a body by the shadow's strength in 256ths being more than
        // nothing. A strength under a 256th rounds to nothing, and Paint then painted the BODY,
        // in full colour, crisp, with no water over it, at the shadow's offset: a second, bright
        // koi a few pixels down and right of the real one for a frame or two, whenever the
        // shadow's strength passed through that sliver on its way to zero (the branches' shade
        // takes it there: "under the branches ... the animals will flash brightly ... and then
        // glitch", 2026-10-27, found on frame 40 of seed 7 at 720p). A shadow too faint to paint
        // is not painted at all; every animal's shadow comes through here.
        if (alpha * 256 < 1) return;
        Paint(fb, onlyWhere, null, 0, soft, null, dx, dy, alpha, null, clip);
    }

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
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]   // (four or five calls per row of every body; not inlined on its own, and inlined it took 0.3 ms off the koi's CPU per frame, pixel for pixel the same: round forty's review)
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
        float invLen = 1 / len, invWid = 1 / wid;                         // two multiplies per pixel instead of two divides (a fin's box is a few hundred pixels, a turtle has forty-odd ovals a frame)
        int murk256 = (int)(murk * 256);
        bool rayed = rays && len > wid * 1.2f;
        // Each row is walked only across the oval's own stretch, not the whole square box (round forty's review: 85
        // percent of the box's pixels were outside the oval, and every one of them read the stencil before the shape
        // test threw it out). Along a row u and v are linear in dx, so u^2 + v^2 = 1 is a quadratic in dx: its two
        // roots are the row's ends, widened a pixel each side; the per-pixel test below still has the last word, so
        // the picture is the same pixel for pixel.
        float kA = ca * invLen, kB = sa * invWid, qA = kA * kA + kB * kB;
        for (int y = y0; y <= y1; y++)
        {
            float dy = y + 0.5f - c.Y, dyCa = dy * ca, dySa = dy * sa;       // the row's share of the turn, done once per row
            float uA = dySa * invLen, vA = dyCa * invWid;
            float qB = 2 * (uA * kA - vA * kB), qC = uA * uA + vA * vA - 1;
            float disc = qB * qB - 4 * qA * qC;
            if (disc < 0) continue;                                           // the row misses the oval
            float sq = MathF.Sqrt(disc), r0 = (-qB - sq) / (2 * qA), r1 = (-qB + sq) / (2 * qA);
            int xs = Math.Max(x0, (int)MathF.Floor(c.X + r0 - 0.5f) - 1), xe = Math.Min(x1, (int)MathF.Ceiling(c.X + r1 - 0.5f) + 1);
            for (int x = xs; x <= xe; x++)
            {
                int idx = y * fb.Width + x;
                if (onlyWhere != null && !onlyWhere[idx]) continue;
                float dx = x + 0.5f - c.X;
                float u = (dx * ca + dySa) * invLen, v = (dyCa - dx * sa) * invWid;
                float q = u * u + v * v;
                if (q >= 1) continue;
                float a = alpha * Math.Clamp((1 - q) * 3, 0, 1) * (rayed ? 0.75f + 0.25f * MathF.Sin(v * 9) : 1f);   // soft edge; fins get faint rays
                if (mark != null && a > 0.02f) mark[idx] = true;
                int rr = r, gg = g, bb = b;
                if (sun != null) { int k = sun[idx]; rr = rr * k >> 8; gg = gg * k >> 8; bb = bb * k >> 8; }
                uint bg = px[idx];
                if (murk > 0)
                {
                    Deepen(ref rr, ref gg, ref bb, murk256);         // darker and greyer by depth (see Deepen), then toward the water
                    uint wc = water != null ? water[idx] : bg;      // toward the water's colour, not whatever is under it (see Paint)
                    rr += (int)((((wc >> 16) & 0xFF) - rr) * murk); gg += (int)((((wc >> 8) & 0xFF) - gg) * murk); bb += (int)(((wc & 0xFF) - bb) * murk);
                }
                px[idx] = FrameBuffer.Blend(bg, rr, gg, bb, a);
            }
        }
    }
}
