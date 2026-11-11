using System.Drawing.Drawing2D;
using Peckworks.Screensavers.Core;

namespace Christmas;

// ============================================================================
// THE SNOWMAN'S PICTURES (YardSnowmanArt.cs)
//
// FEYNMAN VERSION. The snowman is not one picture. He is a stack of parts, each
// painted ONCE (here, on a helper thread while the snow is already falling) and
// stamped every frame wherever that part is right now, like a paper puppet on a
// table where every piece can slide on its own:
//
//   - the BOTTOM and MIDDLE snowballs, each painted at 11 "squash" levels (a
//     little flatter and wider, through to a little taller and thinner), so the
//     whole stack can squash when he lands from a jump and stretch when he leaps.
//     We never scale a sprite per frame (it blurs and costs); we pick the page.
//   - the HEAD ball, plus a flip-book of CARROT NOSES: 25 turns (left three
//     quarters to right three quarters) by 7 nods (looking down to looking up).
//     The carrot is a cone sticking out of the front of the ball. When the head
//     turns, the cone swings round and gets shorter, and when it points straight
//     at you it is just an orange dot with a little tail. That is foreshortening:
//     we compute where the base and the tip of the cone land on the screen for
//     each turn and nod, and paint exactly that.
//   - the FACE: coal eyes (with a lid: open, half, slit, a cross narrowed one, a
//     happy upside-down U), a bead of white that is the glint in each eye, nine
//     coal dots for the smile (which morph into a ring for an "o"), pink cheeks.
//     The face is drawn live on top of the head, at positions worked out from
//     the same turn and nod, so it slides round the ball as the head turns.
//   - the TOP HAT, 13 tilts by 3 levels of snow on it (none, a dusting, a cap),
//     with a sprig of holly on the band.
//   - the SCARF, 32 pages of a striped cloth whose waving grows toward the free
//     end (the technique of the scarecrow's scarf in Thanksgiving).
//   - little things: a ground shadow, a snowflake, sparkles, powder puffs, the
//     "z" of sleep, a joint for the stick arms.
//
// The arms are NOT here: they are stick lines drawn live each frame (a line that
// bends at an elbow is easier to keep smooth than a flip-book of poses).
//
// Light: the moon is up and to the LEFT, so every ball is lit on its upper left
// and shaded blue on its lower right, with a faint bounce of light from the snow
// along the lower edge. No pen round any shape: the edge of a ball is simply the
// darkest part of its shading, which is what makes white show on white.
// ============================================================================

/// <summary>One painted page plus where its anchor point sits inside it (the point the animal's position names).</summary>
internal readonly record struct SnowPage(Sprite Img, int Ox, int Oy);

/// <summary>
/// How big every part of the snowman is, all from his total height T in pixels
/// (foot to the top of the hat). One place for the numbers, so the painter, the
/// resident and the art can never disagree about a size.
/// </summary>
internal readonly struct SnowmanSize
{
    public readonly float T;        // total height, foot to hat top
    public readonly float R0;       // bottom ball radius
    public readonly float R1;       // middle ball radius
    public readonly float R2;       // head radius
    public readonly float Ov;       // how far one ball sinks into the next
    public readonly float ArmLen;   // a whole arm, shoulder to the tip of the wrist
    public readonly float EyeR;     // a coal eye's radius
    public readonly float DotR;     // a coal smile dot's radius
    public readonly float Thick;    // a stick arm's thickness

    public SnowmanSize(float tall)
    {
        T = MathF.Max(10f, tall);
        // Stacked: 0.405 + 0.305 + 0.118 (head radius) minus two overlaps (0.03 each) puts the head's middle
        // 0.768 T up; the hat adds 1.95 head radii above that (0.23 T): about 1.0 T in all.
        R0 = 0.2025f * T;
        R1 = 0.1525f * T;
        R2 = 0.136f * T;                // a storybook head: 15 percent bigger than the first cut (0.118)
        Ov = 0.030f * T;
        ArmLen = 0.34f * T;
        EyeR = MathF.Max(1.0f, 0.14f * R2);
        DotR = MathF.Max(0.9f, 0.085f * R2);
        Thick = MathF.Max(1.4f, 0.017f * T);
    }
}

internal sealed class YardSnowmanArt
{
    // ---- page counts, and the steps between them (the resident quantises with these too) ----
    public const int SquashLevels = 11, SquashMid = 5;          // level k - 5 is -5..+5; scale of the height is 1 + 0.045 * (k - 5)
    public const int YawPages = 25;                             // head turns, left to right
    public const float YawMax = 0.72f;                          // radians: about 41 degrees each way, "three quarters" of the way round to a side
    public const int PitchPages = 7;                            // nods, looking down to looking up
    public const float PitchMin = -0.30f, PitchStep = 0.15f;    // page p is the angle PitchMin + p * PitchStep (page 2 is level)
    public const int ScarfPages = 64;                           // SILKY: 64 flutter pages to a wave (it was 32 and its free end visibly stepped); the scarf is also blended between pages
    public const int HatPages = 13, HatSnowLevels = 3;
    public const float HatStep = 0.12f;                         // radians a page; page 6 is upright
    public const int EyeLevels = 11, EyeSquash = 4;
    public const int EyeCutFrom = 4, EyeCutTo = 9;               // the narrowed (cross) shape exists for these openings
    // How open each eye level is: eleven steps from a slit to fully open (about 0.1 apart, so a lid glides), the last is WIDE (bigger both ways).
    public static readonly float[] EyeOpen = [0.08f, 0.16f, 0.26f, 0.36f, 0.47f, 0.58f, 0.69f, 0.80f, 0.90f, 1.00f, 1.28f];
    public static readonly float[] EyeWidthK = [1.00f, 0.80f, 0.60f, 0.42f];                   // an eye on the side of the ball is narrower: foreshortened

    // ---- colours ----
    private static readonly Color Coal = Color.FromArgb(24, 26, 34);
    private static readonly Color CarrotLit = Color.FromArgb(255, 168, 66);
    private static readonly Color Carrot = Color.FromArgb(244, 126, 26);
    private static readonly Color CarrotDark = Color.FromArgb(196, 82, 16);
    private static readonly Color Bark = Color.FromArgb(104, 72, 46);
    private static readonly Color ScarfRed = Color.FromArgb(192, 40, 54);
    private static readonly Color ScarfCream = Color.FromArgb(240, 230, 210);

    public readonly SnowmanSize Z;
    public readonly SnowPage[] Bottom = new SnowPage[SquashLevels];
    public readonly SnowPage[] Mid = new SnowPage[SquashLevels];
    public readonly SnowPage Head;
    public readonly SnowPage[,] Nose = new SnowPage[YawPages, PitchPages];
    public readonly SnowPage Hole;                                       // the dimple where the carrot was
    public readonly SnowPage[,,] Eye = new SnowPage[2, EyeLevels, EyeSquash];   // [0 round, 1 narrowed][level][squash]
    public readonly SnowPage[] EyeHappy = new SnowPage[EyeSquash];
    public readonly SnowPage Glint, Blush;
    public readonly SnowPage[] Dot = new SnowPage[3];
    public readonly SnowPage[,] Hat = new SnowPage[HatPages, HatSnowLevels];
    public readonly SnowPage[] Scarf = new SnowPage[ScarfPages];
    public readonly SnowPage Shadow, Flake, Joint;
    public readonly SnowPage[] Spark = new SnowPage[2];
    public readonly SnowPage[] Powder = new SnowPage[3];
    public readonly SnowPage[] Zed = new SnowPage[3];

    public YardSnowmanArt(SnowmanSize z)
    {
        Z = z;
        for (int k = 0; k < SquashLevels; k++)
        {
            float sy = 1f + 0.045f * (k - SquashMid), sx = 1f / MathF.Sqrt(sy);       // squash keeps the volume: flatter means wider
            Bottom[k] = BallPage(z.R0 * sx, z.R0 * sy, buttons: false, contactShade: false, z);
            Mid[k] = BallPage(z.R1 * sx, z.R1 * sy, buttons: true, contactShade: true, z);
        }
        Head = BallPage(z.R2, z.R2, buttons: false, contactShade: false, z);
        for (int yw = 0; yw < YawPages; yw++)
            for (int p = 0; p < PitchPages; p++)
                Nose[yw, p] = NosePage(YawOf(yw), PitchMin + p * PitchStep);
        Hole = SoftDot(MathF.Max(1.2f, z.R2 * 0.10f), Color.FromArgb(120, 150, 170, 214));
        PaintEyes();
        Glint = SoftDot(MathF.Max(0.8f, z.EyeR * 0.34f), Color.FromArgb(240, 255, 255, 255));
        Blush = Blob(MathF.Max(2f, z.R2 * 0.30f), MathF.Max(1.5f, z.R2 * 0.18f), Color.FromArgb(150, 255, 138, 166));
        float[] dk = [1f, 0.82f, 0.64f];
        for (int i = 0; i < 3; i++) Dot[i] = SoftDot(z.DotR * dk[i], Coal);
        for (int a = 0; a < HatPages; a++)
            for (int s = 0; s < HatSnowLevels; s++)
                Hat[a, s] = HatPage((a - HatPages / 2) * HatStep, s);
        for (int p = 0; p < ScarfPages; p++) Scarf[p] = ScarfPage(p);
        Shadow = ShadowPage();
        Flake = FlakePage();
        Joint = SoftDot(z.Thick * 0.62f, Bark);
        Spark[0] = SparklePage(MathF.Max(7f, z.T * 0.050f));
        Spark[1] = SparklePage(MathF.Max(5f, z.T * 0.032f));
        float[] pr = [0.011f, 0.017f, 0.025f];
        for (int i = 0; i < 3; i++)
        {
            Sprite puff = Sprite.Glow(Math.Max(2, (int)MathF.Round(z.T * pr[i])), Color.FromArgb(244, 249, 255));
            Powder[i] = new SnowPage(puff, puff.Width / 2, puff.Height / 2);
        }
        float[] zs = [0.085f, 0.115f, 0.150f];                       // big and a deep blue, so a "z" reads on pale snow at 1080p
        for (int i = 0; i < 3; i++) Zed[i] = ZPage(MathF.Max(5f, z.T * zs[i]));
    }

    /// <summary>The head's turn (radians, positive = face toward the viewer's right) that yaw page "i" paints.</summary>
    public static float YawOf(int i) => (i - YawPages / 2) * (2 * YawMax / (YawPages - 1));

    /// <summary>The nearest yaw page to a turn.</summary>
    public static int YawPage(float yaw) => Math.Clamp((int)MathF.Round(yaw / (2 * YawMax / (YawPages - 1))) + YawPages / 2, 0, YawPages - 1);

    /// <summary>The nearest pitch page to a nod.</summary>
    public static int PitchPage(float pitch) => Math.Clamp((int)MathF.Round((pitch - PitchMin) / PitchStep), 0, PitchPages - 1);

    /// <summary>
    /// Where a point of the head's ball lands on the screen, relative to the head's middle, for a given turn and nod.
    /// (lon, lat) are angles on the ball: lon 0 is straight at the viewer, positive to the viewer's right; lat 0 is the
    /// equator, positive up. Returns x right and y DOWN (pixels, in units of the radius r), and z, how much the point
    /// faces the viewer (1 straight at us, 0 on the edge, negative round the back): the face is hidden below about 0.12.
    /// This is the one place the head's geometry lives; the nose pages, the eyes, the mouth and the cheeks all ask it.
    /// </summary>
    public static void FacePoint(float lon, float lat, float yaw, float pitch, float r, out float x, out float y, out float z)
    {
        float px = MathF.Cos(lat) * MathF.Sin(lon), py = MathF.Sin(lat), pz = MathF.Cos(lat) * MathF.Cos(lon);
        // Turn about the vertical axis.
        float x1 = px * MathF.Cos(yaw) + pz * MathF.Sin(yaw), z1 = -px * MathF.Sin(yaw) + pz * MathF.Cos(yaw);
        // Nod about the horizontal axis (positive = chin up, face toward the sky).
        float yUp = py * MathF.Cos(pitch) + z1 * MathF.Sin(pitch), z2 = z1 * MathF.Cos(pitch) - py * MathF.Sin(pitch);
        x = x1 * r;
        y = -yUp * r;
        z = z2;
    }

    // ==================================================================
    //  THE BALLS
    // ==================================================================

    /// <summary>
    /// One snowball, lit from the upper left. The shading is one radial blend whose bright spot sits up and left of the
    /// middle; a soft patch of bounce light at the lower right and a soft moon patch at the upper left are laid over it,
    /// clipped to the ball. The middle ball also carries its three coal buttons, and a soft shade under its bottom edge
    /// (it lies on the bottom ball, which that shade falls on).
    /// </summary>
    private static SnowPage BallPage(float rx, float ry, bool buttons, bool contactShade, SnowmanSize z)
    {
        const int pad = 3;
        float below = contactShade ? ry * 0.34f : 0f;
        int w = (int)MathF.Ceiling(rx * 2) + pad * 2, h = (int)MathF.Ceiling(ry * 2 + below) + pad * 2;
        float cx = w / 2f, cy = pad + ry;
        var spr = Sprite.Paint(w, h, g =>
        {
            if (contactShade)
            {
                using var sp = new GraphicsPath();
                sp.AddEllipse(cx - rx * 0.95f, cy + ry * 0.62f, rx * 1.9f, ry * 0.78f);
                using var sb = new PathGradientBrush(sp) { CenterColor = Color.FromArgb(120, 84, 104, 160), SurroundColors = [Color.FromArgb(0, 84, 104, 160)], Blend = Sprite.SoftFalloff };
                g.FillPath(sb, sp);
            }
            using var path = new GraphicsPath();
            path.AddEllipse(cx - rx, cy - ry, rx * 2, ry * 2);
            using (var br = new PathGradientBrush(path)
            {
                CenterPoint = new PointF(cx - rx * 0.30f, cy - ry * 0.32f),
                InterpolationColors = new ColorBlend
                {
                    Positions = [0f, 0.16f, 0.46f, 0.78f, 1f],
                    // Deeper blues at the edge (the side away from the moon, the lower right, is the darkest) so the ball reads against the pale snow.
                    Colors = [Color.FromArgb(104, 130, 192), Color.FromArgb(138, 164, 214), Color.FromArgb(184, 202, 238), Color.FromArgb(232, 240, 254), Color.FromArgb(252, 253, 255)],
                },
            })
                g.FillPath(br, path);

            // Soft patches laid over it, clipped to the ball so nothing spills past its edge.
            var state = g.Save();
            g.SetClip(path, CombineMode.Intersect);
            SoftPatch(g, cx + rx * 0.66f, cy + ry * 0.70f, rx * 0.62f, ry * 0.46f, Color.FromArgb(78, 186, 208, 250));   // bounce light off the snow below
            SoftPatch(g, cx - rx * 0.78f, cy - ry * 0.70f, rx * 0.46f, ry * 0.36f, Color.FromArgb(70, 255, 255, 255));   // the moon on the rim
            g.Restore(state);

            if (buttons)
            {
                float br2 = MathF.Max(1.0f, rx * 0.078f);
                float[] ys = [-0.10f, 0.28f, 0.62f];
                foreach (float f in ys)
                {
                    float by = cy + ry * f;
                    using var coal = new SolidBrush(Coal);
                    g.FillEllipse(coal, cx - br2, by - br2, br2 * 2, br2 * 2);
                    using var hi = new SolidBrush(Color.FromArgb(150, 150, 160, 190));
                    g.FillEllipse(hi, cx - br2 * 0.55f, by - br2 * 0.65f, br2 * 0.7f, br2 * 0.6f);
                }
            }
        });
        return new SnowPage(spr, (int)MathF.Round(cx), (int)MathF.Round(cy));
    }

    private static void SoftPatch(Graphics g, float cx, float cy, float rx, float ry, Color c)
    {
        using var p = new GraphicsPath();
        p.AddEllipse(cx - rx, cy - ry, rx * 2, ry * 2);
        using var b = new PathGradientBrush(p) { CenterColor = c, SurroundColors = [Color.FromArgb(0, c)], Blend = Sprite.SoftFalloff };
        g.FillPath(b, p);
    }

    private SnowPage ShadowPage()
    {
        float rx = Z.R0 * 1.55f, ry = Z.R0 * 0.38f;
        int w = (int)MathF.Ceiling(rx * 2) + 4, h = (int)MathF.Ceiling(ry * 2) + 4;
        // A clear soft blue ellipse, darkest in the middle (the stack stands over its heart); it is drawn well to the lower right.
        var spr = Sprite.Paint(w, h, g => SoftPatch(g, w / 2f, h / 2f, rx, ry, Color.FromArgb(215, 52, 82, 168)));
        return new SnowPage(spr, w / 2, h / 2);
    }

    // ==================================================================
    //  THE CARROT (25 turns by 7 nods)
    // ==================================================================

    /// <summary>
    /// The carrot for one turn and nod. It is a cone that starts on the ball at lon 0, a touch below the eyes, and points
    /// straight out with a small droop. Both ends are put through FacePoint (the ball's own geometry) and the cone is
    /// painted between them: its base is a circle seen at an angle (an ellipse as flat as the cone points sideways), its
    /// tip a point. Seen from the side that is a long thin triangle; end on it is a round orange disc with a short tail.
    /// </summary>
    private SnowPage NosePage(float yaw, float pitch)
    {
        float r = Z.R2;
        // The cone's axis in the head's own space: out along the front (z) with a droop, normalised.
        const float lat0 = -0.06f;
        float ax = 0f, ay = MathF.Sin(lat0) - 0.12f, az = MathF.Cos(lat0);
        float al = MathF.Sqrt(ax * ax + ay * ay + az * az); ax /= al; ay /= al; az /= al;
        float len = 0.90f * r;
        // Base and tip as points in the head's space (the base is on the ball), then turned and nodded like any face point.
        float bx0 = 0f, by0 = MathF.Sin(lat0), bz0 = MathF.Cos(lat0);
        void Project(float x0, float y0, float z0, out float sx, out float sy, out float sz)
        {
            float x1 = x0 * MathF.Cos(yaw) + z0 * MathF.Sin(yaw), z1 = -x0 * MathF.Sin(yaw) + z0 * MathF.Cos(yaw);
            float yUp = y0 * MathF.Cos(pitch) + z1 * MathF.Sin(pitch), z2 = z1 * MathF.Cos(pitch) - y0 * MathF.Sin(pitch);
            sx = x1 * r; sy = -yUp * r; sz = z2;
        }
        Project(bx0, by0, bz0, out float bx, out float by, out _);
        Project(bx0 * 1f + ax * len / r, by0 + ay * len / r, bz0 + az * len / r, out float tx, out float ty, out _);
        // Axis on the screen, and how much the cone points at the viewer (its z after turning).
        Project(ax, ay, az, out float axs, out float ays, out float azs);
        float dx = tx - bx, dy = ty - by, dl = MathF.Sqrt(dx * dx + dy * dy);
        float nx, ny;
        if (dl < 1e-3f) { nx = 0; ny = 1; } else { nx = -dy / dl; ny = dx / dl; }       // the screen normal of the axis (the base ellipse's long way)
        float wb = MathF.Max(1.5f, 0.17f * r);                                           // the carrot's thickness at its base
        float minor = wb * MathF.Max(0.30f, MathF.Abs(azs));                              // the base ellipse's short way, along the axis
        float ux = dl < 1e-3f ? 0 : dx / dl, uy = dl < 1e-3f ? -1 : dy / dl;
        // The base ellipse as a polygon.
        const int n = 14;
        var ell = new PointF[n];
        for (int i = 0; i < n; i++)
        {
            float a = i * MathF.Tau / n;
            ell[i] = new PointF(bx + nx * wb * 0.5f * MathF.Cos(a) + ux * minor * 0.5f * MathF.Sin(a), by + ny * wb * 0.5f * MathF.Cos(a) + uy * minor * 0.5f * MathF.Sin(a));
        }
        var tri = new PointF[] { new(bx + nx * wb * 0.5f, by + ny * wb * 0.5f), new(tx, ty), new(bx - nx * wb * 0.5f, by - ny * wb * 0.5f) };
        // Page bounds in head-relative pixels.
        float minX = tx, maxX = tx, minY = ty, maxY = ty;
        foreach (PointF p in ell) { minX = MathF.Min(minX, p.X); maxX = MathF.Max(maxX, p.X); minY = MathF.Min(minY, p.Y); maxY = MathF.Max(maxY, p.Y); }
        const int pad = 2;
        int ox = (int)MathF.Ceiling(-minX) + pad, oy = (int)MathF.Ceiling(-minY) + pad;
        int w = ox + (int)MathF.Ceiling(maxX) + pad + 1, h = oy + (int)MathF.Ceiling(maxY) + pad + 1;
        var spr = Sprite.Paint(w, h, g =>
        {
            g.TranslateTransform(ox, oy);
            using var body = new SolidBrush(Carrot);
            g.FillPolygon(body, tri);
            g.FillPolygon(body, ell);
            // The lit side (upper left, where the moon is) and the shaded side (lower right): thin slivers along the two edges.
            // Which edge of the cone is "up and left" is the one whose normal points that way.
            float side = (-nx - ny) >= 0 ? 1f : -1f;                                   // +1: the +normal edge is the lit one
            PointF litBase = new(bx + side * nx * wb * 0.5f, by + side * ny * wb * 0.5f), darkBase = new(bx - side * nx * wb * 0.5f, by - side * ny * wb * 0.5f);
            PointF mid(PointF a, PointF b2, float t) => new(a.X + (b2.X - a.X) * t, a.Y + (b2.Y - a.Y) * t);
            using (var lit = new SolidBrush(CarrotLit))
                g.FillPolygon(lit, [litBase, mid(litBase, new PointF(tx, ty), 0.95f), mid(new PointF(bx, by), new PointF(tx, ty), 0.80f), new PointF(bx, by)]);
            using (var dark = new SolidBrush(CarrotDark))
                g.FillPolygon(dark, [darkBase, mid(darkBase, new PointF(tx, ty), 0.95f), mid(new PointF(bx, by), new PointF(tx, ty), 0.80f), new PointF(bx, by)]);
            // Three rings round the carrot (a carrot has them), only worth it when there is length to put them on.
            if (dl > wb * 1.2f)
                using (var ring = new Pen(Color.FromArgb(110, 160, 62, 10), MathF.Max(1f, wb * 0.12f)))
                    foreach (float t in new[] { 0.28f, 0.52f, 0.74f })
                    {
                        float wk = wb * 0.5f * (1f - t * 0.9f);
                        PointF c = mid(new PointF(bx, by), new PointF(tx, ty), t);
                        g.DrawLine(ring, c.X - nx * wk * 0.8f, c.Y - ny * wk * 0.8f, c.X + nx * wk * 0.8f, c.Y + ny * wk * 0.8f);
                    }
            // The end-on bright spot: a carrot pointing at us shows its cut end as a lighter disc.
            if (azs > 0.55f)
            {
                float er = wb * 0.30f;
                using var cut = new SolidBrush(Color.FromArgb(230, 255, 190, 96));
                g.FillEllipse(cut, tx - er, ty - er, er * 2, er * 2);
            }
        });
        return new SnowPage(spr, ox, oy);
    }

    // ==================================================================
    //  THE EYES
    // ==================================================================

    private void PaintEyes()
    {
        float er = Z.EyeR;
        int half = (int)MathF.Ceiling(er * 1.3f * 1.15f) + 2;
        int size = half * 2 + 1;                                                        // ODD, so the anchor is a true pixel and a mirrored page lands on the same spot
        for (int sq = 0; sq < EyeSquash; sq++)
        {
            float wk = EyeWidthK[sq];
            for (int lv = 0; lv < EyeLevels; lv++)
            {
                float big = lv == EyeLevels - 1 ? 1.25f : 1f;
                float rx = er * wk * big, ry = MathF.Max(0.55f, er * 1.12f * big * (lv == EyeLevels - 1 ? 1f : EyeOpen[lv]));
                for (int cut = 0; cut < 2; cut++)
                {
                    if (cut == 1 && (lv < EyeCutFrom || lv > EyeCutTo)) continue;                   // only the middle openings come in a narrowed shape
                    bool slant = cut == 1;
                    Eye[cut, lv, sq] = new SnowPage(Sprite.Paint(size, size, g =>
                    {
                        float c = half + 0.5f;
                        using var path = new GraphicsPath();
                        path.AddEllipse(c - rx, c - ry, rx * 2, ry * 2);
                        var state = g.Save();
                        if (slant)
                        {
                            // A lid pulled down from above, slanted so the inner end (toward the nose, the right of the LEFT eye) is lower.
                            using var keep = new GraphicsPath();
                            keep.AddPolygon([new PointF(c - rx - 2, c - ry * 0.10f - ry * 0.55f), new PointF(c + rx + 2, c - ry * 0.10f + ry * 0.45f), new PointF(c + rx + 2, c + ry + 2), new PointF(c - rx - 2, c + ry + 2)]);
                            g.SetClip(keep, CombineMode.Intersect);
                        }
                        using (var b = new SolidBrush(Coal)) g.FillPath(b, path);
                        using (var sheen = new SolidBrush(Color.FromArgb(70, 120, 126, 150)))
                            g.FillEllipse(sheen, c - rx * 0.1f, c + ry * 0.15f, rx * 0.85f, ry * 0.65f);
                        g.Restore(state);
                    }), half, half);
                }
            }
            // The happy eye: an upside-down U, a thick arc with round ends.
            float hw = er * wk * 1.15f;
            EyeHappy[sq] = new SnowPage(Sprite.Paint(size, size, g =>
            {
                float c = half + 0.5f;
                using var pen = new Pen(Coal, MathF.Max(1.2f, er * 0.62f)) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                g.DrawArc(pen, c - hw, c - hw * 0.8f, hw * 2, hw * 1.6f, 200, 140);
            }), half, half);
        }
    }

    // ==================================================================
    //  THE HAT
    // ==================================================================

    /// <summary>
    /// The top hat, turned about the middle of its brim by "angle" (radians, positive = clockwise on the screen, so the
    /// crown leans to the viewer's right). snow: 0 none, 1 a dusting along the brim, 2 a cap on the crown and a heavy brim.
    /// The sprig of holly sits on the band at the front left.
    /// </summary>
    private SnowPage HatPage(float angle, int snow)
    {
        float r = Z.R2;
        int size = (int)MathF.Ceiling(r * 3.1f) | 1;
        float c = size / 2f;
        var spr = Sprite.Paint(size, size, g =>
        {
            g.TranslateTransform(c, c);
            g.RotateTransform(angle * 180f / MathF.PI);
            float cw = r * 0.60f, top = -r * 1.10f, bh = r * 0.17f, bw = r * 1.0f;
            // The brim first (a flat ellipse seen nearly edge on), then the crown over its middle, then the brim's front lip over the crown's foot.
            using var brimB = new LinearGradientBrush(new RectangleF(-bw, -bh, bw * 2, bh * 2), Color.FromArgb(70, 66, 88), Color.FromArgb(24, 22, 32), 0f);
            g.FillEllipse(brimB, -bw, -bh, bw * 2, bh * 2);
            var crown = new GraphicsPath();
            crown.AddPolygon([new PointF(-cw * 0.94f, top + r * 0.10f), new PointF(-cw * 0.82f, top), new PointF(cw * 0.82f, top), new PointF(cw * 0.94f, top + r * 0.10f), new PointF(cw, 0), new PointF(-cw, 0)]);
            using (var crownB = new LinearGradientBrush(new RectangleF(-cw, top, cw * 2, -top), Color.FromArgb(78, 72, 98), Color.FromArgb(26, 24, 36), 0f))
                g.FillPath(crownB, crown);
            using (var bandB = new SolidBrush(Color.FromArgb(188, 38, 52)))
                g.FillPolygon(bandB, [new PointF(-cw * 0.99f, -r * 0.40f), new PointF(cw * 0.99f, -r * 0.40f), new PointF(cw, -r * 0.14f), new PointF(-cw, -r * 0.14f)]);
            using (var lip = new SolidBrush(Color.FromArgb(32, 28, 42)))
                g.FillPie(lip, -bw, -bh, bw * 2, bh * 2, 0, 180);
            // Holly: two leaves and three berries, at the front left of the band.
            using (var leaf = new SolidBrush(Color.FromArgb(48, 124, 66)))
            using (var leafLit = new SolidBrush(Color.FromArgb(94, 170, 96)))
            {
                var st = g.Save();
                g.TranslateTransform(-cw * 0.46f, -r * 0.30f);
                g.RotateTransform(-35);
                g.FillEllipse(leaf, -r * 0.28f, -r * 0.075f, r * 0.34f, r * 0.15f);
                g.RotateTransform(80);
                g.FillEllipse(leafLit, -r * 0.06f, -r * 0.075f, r * 0.34f, r * 0.15f);
                g.Restore(st);
            }
            using (var berry = new SolidBrush(Color.FromArgb(214, 34, 48)))
            using (var gleam = new SolidBrush(Color.FromArgb(220, 255, 220, 220)))
            {
                float br = MathF.Max(1f, r * 0.085f);
                PointF[] bs = [new(-cw * 0.42f, -r * 0.30f), new(-cw * 0.30f, -r * 0.24f), new(-cw * 0.40f, -r * 0.20f)];
                foreach (PointF p in bs)
                {
                    g.FillEllipse(berry, p.X - br, p.Y - br, br * 2, br * 2);
                    g.FillEllipse(gleam, p.X - br * 0.5f, p.Y - br * 0.6f, br * 0.55f, br * 0.5f);
                }
            }
            // Snow on it, white on black so it reads.
            if (snow >= 1)
            {
                using var white = new SolidBrush(Color.FromArgb(snow == 1 ? 215 : 240, 240, 246, 255));
                float thick = snow == 1 ? r * 0.07f : r * 0.14f;
                using var pen = new Pen(Color.FromArgb(snow == 1 ? 215 : 240, 240, 246, 255), thick) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                g.DrawArc(pen, -bw * 0.97f, -bh * 0.97f, bw * 1.94f, bh * 1.94f, 188, 164);              // along the top edge of the brim
                if (snow >= 2)
                {
                    g.FillEllipse(white, -cw * 0.95f, top - r * 0.13f, cw * 1.9f, r * 0.30f);             // the cap on the crown
                    g.FillEllipse(white, -cw * 0.55f, top - r * 0.20f, cw * 0.9f, r * 0.24f);
                    g.FillEllipse(white, -cw * 1.00f, top + r * 0.04f, r * 0.22f, r * 0.20f);             // a clump sliding down the side
                }
            }
        });
        return new SnowPage(spr, (int)MathF.Round(c), (int)MathF.Round(c));
    }

    // ==================================================================
    //  THE SCARF
    // ==================================================================

    /// <summary>
    /// The scarf at one phase of its flutter: a chunky striped band round the neck (a smile-shaped strip), a knot at the
    /// front right, a short tail hanging and a long tail streaming off to the right in the breeze. The long tail's wave
    /// grows from nothing at the knot to its biggest at the free end, travelling toward the end (the scarecrow's scarf
    /// does the same): a flag does not wave at its pole.
    /// The page's anchor (Ox, Oy) is the middle of the neck.
    /// </summary>
    private SnowPage ScarfPage(int page)
    {
        float T = Z.T, ph = page * MathF.Tau / ScarfPages;
        float th = 0.070f * T, bw = 0.150f * T, bh = 0.045f * T;
        int w = (int)(0.98f * T) + 6, h = (int)(0.66f * T) + 6;
        float ax = 0.30f * T + 3, ay = 0.10f * T + 3;
        var spr = Sprite.Paint(w, h, g =>
        {
            g.TranslateTransform(ax, ay);

            // A strip along a centre line: the two edges, the whole strip in red, the stripes over it in cream, a lit and a
            // shaded half. "stripeEvery" is how many steps a stripe is.
            void Strip(PointF[] c, float w0, float w1, int stripeEvery)
            {
                int steps = c.Length - 1;
                var left = new PointF[steps + 1]; var right = new PointF[steps + 1];
                for (int i = 0; i <= steps; i++)
                {
                    PointF a = c[Math.Max(0, i - 1)], b = c[Math.Min(steps, i + 1)];
                    float dx = b.X - a.X, dy = b.Y - a.Y, l = MathF.Sqrt(dx * dx + dy * dy) + 1e-5f;
                    float hw = (w0 + (w1 - w0) * i / steps) * 0.5f;
                    left[i] = new PointF(c[i].X - dy / l * hw, c[i].Y + dx / l * hw);
                    right[i] = new PointF(c[i].X + dy / l * hw, c[i].Y - dx / l * hw);
                }
                PointF[] Outline(int from, int to)
                {
                    var o = new PointF[(to - from + 1) * 2];
                    for (int i = from; i <= to; i++) { o[i - from] = left[i]; o[o.Length - 1 - (i - from)] = right[i]; }
                    return o;
                }
                using (var red = new SolidBrush(ScarfRed)) g.FillPolygon(red, Outline(0, steps));            // ONE polygon: no seams between pieces
                using (var cream = new SolidBrush(ScarfCream))
                    for (int s = stripeEvery; s < steps; s += stripeEvery * 2)
                        g.FillPolygon(cream, Outline(s, Math.Min(steps, s + stripeEvery)));
                // The lit upper edge and the shaded lower edge, as soft tints over both colours.
                var lit = new PointF[(steps + 1) * 2]; var shade = new PointF[(steps + 1) * 2];
                for (int i = 0; i <= steps; i++)
                {
                    lit[i] = left[i]; lit[lit.Length - 1 - i] = c[i];
                    shade[i] = c[i]; shade[shade.Length - 1 - i] = right[i];
                }
                using (var lb = new SolidBrush(Color.FromArgb(34, 255, 255, 255))) g.FillPolygon(lb, lit);
                using (var sb = new SolidBrush(Color.FromArgb(52, 40, 0, 24))) g.FillPolygon(sb, shade);
            }

            // The long tail: leaves the knot, sags a little, and waves more and more toward its end.
            float kx = -bw * MathF.Cos(0.80f * MathF.PI), ky = bh * MathF.Sin(0.80f * MathF.PI);       // the knot, on the band
            const int ls = 24;
            var lt = new PointF[ls + 1];
            float lenL = 0.50f * T;
            for (int i = 0; i <= ls; i++)
            {
                float s = i / (float)ls;
                float amp = 0.085f * T * MathF.Pow(s, 1.35f);
                lt[i] = new PointF(kx + s * lenL * 0.98f, ky + s * lenL * 0.12f + 0.05f * T * s * s + amp * MathF.Sin(ph - s * MathF.Tau * 1.15f));
            }
            Strip(lt, th * 0.86f, th * 1.04f, 3);
            // The short tail hangs from the knot, swaying a little, a quarter beat behind.
            const int ss = 12;
            var st = new PointF[ss + 1];
            float lenS = 0.22f * T;
            for (int i = 0; i <= ss; i++)
            {
                float s = i / (float)ss;
                st[i] = new PointF(kx + 0.01f * T + 0.028f * T * s * MathF.Sin(ph + 1.4f - s * 2.4f) + 0.012f * T * s, ky + s * lenS);
            }
            Strip(st, th * 0.82f, th * 0.92f, 3);
            // The band round the neck, drawn last so it sits over the roots of both tails.
            const int bs = 26;
            var band = new PointF[bs + 1];
            for (int i = 0; i <= bs; i++)
            {
                float a = MathF.PI * (0.03f + 0.94f * i / bs);
                band[i] = new PointF(-bw * MathF.Cos(a), bh * MathF.Sin(a) + 0.006f * T);
            }
            Strip(band, th, th, 3);
            // The knot: a plump bump over the join.
            using (var kn = new SolidBrush(Color.FromArgb(176, 34, 48)))
                g.FillEllipse(kn, kx - th * 0.52f, ky - th * 0.46f, th * 1.04f, th * 0.92f);
            using (var kl = new SolidBrush(Color.FromArgb(60, 255, 255, 255)))
                g.FillEllipse(kl, kx - th * 0.36f, ky - th * 0.40f, th * 0.55f, th * 0.34f);
        });
        return new SnowPage(spr, (int)MathF.Round(ax), (int)MathF.Round(ay));
    }

    // ==================================================================
    //  LITTLE THINGS
    // ==================================================================

    private static SnowPage SoftDot(float r, Color c)
    {
        int d = (int)MathF.Ceiling(r * 2) + 4;
        var spr = Sprite.Paint(d, d, g =>
        {
            using var b = new SolidBrush(c);
            g.FillEllipse(b, d / 2f - r, d / 2f - r, r * 2, r * 2);
        });
        return new SnowPage(spr, d / 2, d / 2);
    }

    private static SnowPage Blob(float rx, float ry, Color c)
    {
        int w = (int)MathF.Ceiling(rx * 2) + 4, h = (int)MathF.Ceiling(ry * 2) + 4;
        var spr = Sprite.Paint(w, h, g => SoftPatch(g, w / 2f, h / 2f, rx, ry, c));
        return new SnowPage(spr, w / 2, h / 2);
    }

    /// <summary>A six-armed snowflake, bigger than the falling snow, so the eye can follow the one he catches.</summary>
    private SnowPage FlakePage()
    {
        int s = (int)MathF.Max(8f, Z.T * 0.060f) | 1;
        float c = s / 2f, arm = s * 0.46f;
        var spr = Sprite.Paint(s, s, g =>
        {
            using var pen = new Pen(Color.FromArgb(250, 244, 250, 255), MathF.Max(1f, s * 0.10f)) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            using var fine = new Pen(Color.FromArgb(220, 244, 250, 255), MathF.Max(1f, s * 0.07f)) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            for (int i = 0; i < 6; i++)
            {
                float a = i * MathF.PI / 3f;
                float ca = MathF.Cos(a), sa = MathF.Sin(a);
                g.DrawLine(pen, c, c, c + ca * arm, c + sa * arm);
                float bx = c + ca * arm * 0.58f, by = c + sa * arm * 0.58f;
                foreach (float d in new[] { -0.9f, 0.9f })
                    g.DrawLine(fine, bx, by, bx + MathF.Cos(a + d) * arm * 0.30f, by + MathF.Sin(a + d) * arm * 0.30f);
            }
        });
        return new SnowPage(spr, s / 2, s / 2);
    }

    /// <summary>A four-point gold-white sparkle over a soft halo (a fallen star of dust).</summary>
    private static SnowPage SparklePage(float size)
    {
        int s = (int)size | 1;
        float c = s / 2f, thin = MathF.Max(0.7f, s * 0.07f);
        var spr = Sprite.Paint(s, s, g =>
        {
            SoftPatch(g, c, c, s * 0.46f, s * 0.46f, Color.FromArgb(90, 255, 226, 150));
            using var ray = new SolidBrush(Color.FromArgb(240, 255, 244, 205));
            g.FillPolygon(ray, [new PointF(0, c), new PointF(c, c - thin), new PointF(s, c), new PointF(c, c + thin)]);
            g.FillPolygon(ray, [new PointF(c, 0), new PointF(c + thin, c), new PointF(c, s), new PointF(c - thin, c)]);
            using var core = new SolidBrush(Color.White);
            g.FillEllipse(core, c - thin * 1.3f, c - thin * 1.3f, thin * 2.6f, thin * 2.6f);
        });
        return new SnowPage(spr, s / 2, s / 2);
    }

    /// <summary>A "z" for sleep: three strokes.</summary>
    private static SnowPage ZPage(float size)
    {
        int s = (int)size + 5;
        var spr = Sprite.Paint(s, s, g =>
        {
            float m = 2.5f, e = s - 2.5f;
            using var pen = new Pen(Color.FromArgb(250, 70, 98, 196), MathF.Max(1.5f, size * 0.20f)) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
            g.DrawLines(pen, [new PointF(m, m), new PointF(e, m), new PointF(m, e), new PointF(e, e)]);
        });
        return new SnowPage(spr, s / 2, s / 2);
    }
}
