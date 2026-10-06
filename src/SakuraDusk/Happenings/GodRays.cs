using Peckworks.Screensavers.Core;

namespace SakuraDusk.Happenings;

/// <summary>
/// God rays, which the Japanese call "tenshi no hashigo", the angel's ladder:
/// when the sun is low and there is a little haze, broad soft beams of light
/// seem to fan up from it across the sky, with darker gaps between them.
///
/// FEYNMAN VERSION: think of a torch shone through dusty air. The dust
/// lights up where the beam passes, so you see the beam itself. Here each
/// beam is a long wedge that starts as a thin point at the sun and gets wider
/// the farther it goes, and is brightest at the sun and fades to nothing at
/// its far end and at both sides.
///
/// HOW IT IS DRAWN (a deliberate change from "paint a sprite per ray"): a
/// sprite for a slanted ray is a big square sheet that is mostly empty, and
/// seven of them cost several milliseconds a frame on a 4K screen just to be
/// walked over. So instead each ray is worked out pixel by pixel, but ONLY for
/// the pixels inside its wedge (about a thirtieth of the sheet). For a pixel
/// we ask two questions: how far along the ray is it, and how far to the side
/// of the middle line? Two small lookup tables (made once) turn those two
/// answers into "how bright", and the light is blended onto the sky. It
/// rotates and breathes for free, because the angle and the brightness are
/// just numbers read from t. Only the open sky takes the light (the stencil),
/// so the hills, the pagoda and the branches stay in front.
/// </summary>
internal sealed class GodRays : Happening
{
    private const int Tab = 256;                                  // lookup table resolution

    private struct Ray { public float Angle, Len, HalfStart, HalfEnd, Phase; }

    private readonly DuskScenery _s;
    private readonly int[] _along = new int[Tab + 1];             // brightness (0..256) by how far along the ray, 0 at the sun to 1 at the tip
    private readonly int[] _across = new int[Tab + 1];            // brightness (0..256) by how far to the side, 0 at the middle line to 1 at the edge
    private readonly float[] _recipHalf = new float[Tab + 1];    // scratch table, refilled for each ray (Draw runs on one thread)
    private Ray[] _rays = [];

    public override float Seconds => 14f;
    public override string? Claims => "sun";

    public GodRays(DuskScenery s)
    {
        _s = s;
        for (int i = 0; i <= Tab; i++)
        {
            float u = i / (float)Tab;
            // Along: eases in over the first 6% (so there is no hard point at the
            // sun), then fades away toward the tip.
            float edge = Math.Min(1f, u / 0.06f);
            _along[i] = (int)(256 * edge * edge * (3 - 2 * edge) * MathF.Pow(1 - u, 1.1f));
            // Across: full in the middle, a soft bell to nothing at the edge.
            float k = 1 - u * u;
            _across[i] = (int)(256 * k * k);
        }
    }

    public override void Begin(Random rng)
    {
        int n = rng.Next(7, 10);
        _rays = new Ray[n];
        float u = _s.U;
        float spread = 136f * MathF.PI / 180f;                    // the fan covers about 136 degrees, straight up in the middle
        for (int i = 0; i < n; i++)
        {
            float slot = (i + 0.5f) / n - 0.5f;                   // evenly spaced, then shaken so they look natural
            _rays[i] = new Ray
            {
                Angle = slot * spread + (float)(rng.NextDouble() - 0.5) * spread / n * 0.8f,
                Len = u * (0.5f + 0.5f * (float)rng.NextDouble()),
                HalfStart = Math.Max(1f, u * 0.004f),
                HalfEnd = Math.Max(3f, u * (0.02f + 0.035f * (float)rng.NextDouble())),
                Phase = (float)(rng.NextDouble() * Math.Tau),
            };
        }
    }

    public override void Draw(FrameBuffer fb, float t)
    {
        float on = Fade(t, Seconds, 4f, 4f);
        PointF sun = _s.Sun.At;
        foreach (var r in _rays)
        {
            // Each ray breathes at its own pace, and the whole fan leans a
            // degree or two back and forth, so nothing sits still.
            float breathe = 0.7f + 0.3f * (float)Math.Sin(t * 0.55 + r.Phase);
            int strength = (int)(0.25f * on * breathe * 256);
            if (strength <= 0) continue;
            float angle = r.Angle + 0.03f * (float)Math.Sin(t * 0.3 + r.Phase);
            Wedge(fb, sun.X, sun.Y, angle, r, strength);
        } { 
        }
    }

    /// <summary>
    /// Lights one wedge: only the pixels inside it, only where the sky is open.
    /// On a big screen the brightness is worked out once per small square of
    /// pixels (3 by 3 at 4K) and shared by the square. A beam is soft, so you
    /// cannot see it, and it makes the work a quarter as much. The stencil is
    /// still checked for every pixel, so the hills keep their exact edges.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private void Wedge(FrameBuffer fb, float sx, float sy, float angle, Ray r, int strength)
    {
        int st = Math.Max(1, fb.Height / 720);                    // the size of the square sharing one brightness (1 at 1080p, 3 at 4K)
        float dx = MathF.Sin(angle), dy = -MathF.Cos(angle);      // the way the ray points (up when angle is 0)
        float nx = -dy, ny = dx;                                  // sideways, at a right angle to that
        float tipX = sx + dx * r.Len, tipY = sy + dy * r.Len;
        // The wedge is a triangle: the sun and the two corners at the tip. Rows
        // of the triangle give each row's left and right end, so we never walk
        // the empty corners of a bounding box.
        (float x, float y)[] tri =
            [(sx, sy), (tipX + nx * r.HalfEnd, tipY + ny * r.HalfEnd), (tipX - nx * r.HalfEnd, tipY - ny * r.HalfEnd)];

        int yMax = Math.Min(fb.Height - 1, _s.Height - 1);
        int yTop = Math.Max(0, (int)MathF.Floor(Math.Min(tri[0].y, Math.Min(tri[1].y, tri[2].y))));
        int yBot = Math.Min(yMax, (int)MathF.Ceiling(Math.Max(tri[0].y, Math.Max(tri[1].y, tri[2].y))));
        uint[] px = fb.Pixels;
        bool[] sky = _s.OpenSky;
        float lenToTab = Tab / r.Len;
        for (int i = 0; i <= Tab; i++)                            // 1 / half-width at each step along the ray (no division per pixel)
            _recipHalf[i] = Tab / (r.HalfStart + (r.HalfEnd - r.HalfStart) * i / Tab);
        const uint GoldRB = (255u << 16) | 160u, GoldG = 220u << 8;

        for (int y = yTop; y <= yBot; y += st)
        {
            float fy = y + st * 0.5f, lo = float.MaxValue, hi = float.MinValue;
            for (int e = 0; e < 3; e++)
            {
                var a = tri[e]; var b = tri[(e + 1) % 3];
                if ((fy < a.y && fy < b.y) || (fy > a.y && fy > b.y) || a.y == b.y) continue;
                float x = a.x + (fy - a.y) * (b.x - a.x) / (b.y - a.y);
                lo = Math.Min(lo, x); hi = Math.Max(hi, x);
            }
            if (lo > hi) continue;
            int x0 = Math.Max(0, (int)MathF.Floor(lo) - st), x1 = Math.Min(fb.Width - 1, (int)MathF.Ceiling(hi) + st);
            float rely = fy - sy, rowD = rely * dy, rowO = rely * ny;
            for (int x = x0; x <= x1; x += st)
            {
                float relx = x + st * 0.5f - sx;
                float dist = relx * dx + rowD;                    // how far along the ray
                if (dist <= 0 || dist >= r.Len) continue;
                int fi = (int)(dist * lenToTab);
                int vi = (int)(MathF.Abs(relx * nx + rowO) * _recipHalf[fi]);   // how far to the side, as a fraction of the width here
                if (vi >= Tab) continue;
                uint A = (uint)((_along[fi] * _across[vi] >> 8) * strength >> 8);
                if (A == 0) continue;
                uint inv = 256 - A;
                for (int by = y; by < y + st && by <= yMax; by++)
                    for (int bx = x; bx < x + st && bx < fb.Width; bx++)
                    {
                        int i = by * fb.Width + bx;
                        if (!sky[i]) continue;
                        uint bg = px[i];
                        px[i] = ((((bg & 0xFF00FF) * inv + GoldRB * A) >> 8) & 0xFF00FF) | ((((bg & 0x00FF00) * inv + GoldG * A) >> 8) & 0x00FF00);
                    }
            }
        }
    }
}