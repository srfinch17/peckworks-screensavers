using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Numerics;
using Peckworks.Screensavers.Core;

namespace Christmas;

// ============================================================================
// REINDEER ART: every picture of the herd, painted once on a helper thread.
//
// FEYNMAN VERSION. A reindeer on screen is TWO stickers stamped together: a
// BODY sticker (torso, legs, tail) and a HEAD sticker (neck, mane, head, ears,
// antlers). The body book has a page for every pose; the head book has a page
// for every way the neck can tilt, from "nose up at the sky" to "nose down in
// a hole in the snow". Each body page remembers where the neck starts on it,
// so the head rides the body's rocking walk and nods with it.
//
// LOOKED UP FIRST (a caribou, Rangifer tarandus, written down before any code;
// every number is in "body heights": the shoulder height is 1.0, so a body is
// painted once at any size and the numbers keep their meaning):
//
//   - Shoulder height 1.1 m. Body (chest to rump) about 1.35 heights long, so
//     with the head out in front the animal is 1.9 m. Stocky: a deep chest
//     (the belly line sits at about half the shoulder height), a back that is
//     level, a little higher at the withers than at the rump.
//   - LEGS are short and thick for a deer: 0.58 high at the shoulder joint, the
//     hind hip at 0.64. The foreleg has one visible joint (the "knee", really
//     the wrist) that bends FORWARD when the leg lifts. The hind leg zigzags:
//     the stifle bends forward, the HOCK bends BACKWARD, and the long cannon
//     bone runs from the hock down to the foot. The lower legs are pale.
//   - HOOVES are big and round (a caribou's hoof is a snowshoe).
//   - NECK deep and thick (0.40 long, 0.36 wide at the root, a bull more), with
//     a long pale MANE that hangs under it like a beard. HEAD long and broad,
//     0.45 long with a wide muzzle and a big dark nose pad; ears short and
//     rounded. TAIL a short white tuft.
//   - ANTLERS (both sexes grow them, which is rare in deer): a main beam that
//     sweeps up and BACK from the brow, then curves forward at the top; a flat
//     "shovel" tine over the face (the brow tine) and a second forward tine;
//     a bull's rack is tall and wide with a palmate top, a cow's is a smaller,
//     thinner version, a calf has none yet.
//   - GAIT: a walk in the LATERAL sequence (left hind, left fore, right hind,
//     right fore, each a quarter of the stride after the last) and a trot in
//     DIAGONAL pairs with a moment in the air.
//   - COAT: grey-brown, with a cream neck and mane, a pale rump patch, a pale
//     belly and pale lower legs. At night against dark pines the dark coat would
//     vanish, so the pale parts are kept pale and a thin moonlit rim runs along
//     the back.
//
// Everything faces RIGHT; the engine's mirror stamp turns it to face left.
// ============================================================================

internal enum ReindeerKind { Bull, Cow, Calf }

/// <summary>One body page and where the neck's root is on it (body heights from the middle of the body, up from the ground).</summary>
internal sealed class RPage
{
    public Sprite S = null!;
    public float NX, NY;
    public int OX, OY;            // the pixel of this (cropped) sprite that is the body's middle on the ground, or the neck's root for a head page
}

/// <summary>Every flip-book of one animal (its own size: one animal stays on its own row of the field).</summary>
internal sealed class ReindeerSet
{
    public ReindeerKind Kind;
    public int H;                                           // shoulder height in pixels
    public int BodyW, BodyH, BodyOx, BodyOy;                // the body sheet and the pixel that is the middle of the body on the ground
    public int HeadW, HeadH, HeadOx, HeadOy;                // the head sheet and the pixel that is the neck's root
    public RPage[] Walk = new RPage[ReindeerArt.WalkPages];
    public RPage[] Trot = new RPage[ReindeerArt.TrotPages];
    public RPage[] Settle = new RPage[ReindeerArt.SettlePages];   // the walk's rest page to the square stance, one foot at a time
    public RPage[] Paw = new RPage[ReindeerArt.PawPages];         // one scrape with the near foreleg
    public RPage[] Down = new RPage[ReindeerArt.LiePages];        // lying down: forelegs fold first
    public RPage[] Up = new RPage[ReindeerArt.LiePages];          // getting up: the hind end first
    public RPage[] Shake = new RPage[ReindeerArt.ShakePages];     // the torso twisted from one side to the other
    public RPage[] Gam = new RPage[ReindeerArt.GamPages];         // the calf's play poses
    public RPage[] TrotIn = new RPage[ReindeerArt.TrotInPages];     // the square stance to the trot's first page (and back): a start and a stop that share pages with the trot       // the square stance narrowed about the feet
    public RPage[,] Head = new RPage[ReindeerArt.HeadPages, ReindeerArt.HeadKinds];
    public RPage?[,,] Fig = new RPage?[2, ReindeerArt.TurnLevels, ReindeerArt.TurnSteps + 1];   // the turn: whole animals (body and head together) seen from every angle of a half turn; [side (0 toward us, 1 away), head pitch level, step]; steps 0 and TurnSteps are null (the ordinary standing pages are used there)
    public int FigW, FigH, FigOx, FigOy;                            // the turn sheet and the pixel that is the body's middle on the ground
    public PointF[] Nose = new PointF[ReindeerArt.HeadPages];     // the nostril on each head page (body heights from the neck's root, y up)
    public Sprite Shadow = null!, ShadowLie = null!;              // the soft blue shadow under it, a separate stamp (it stays on the ground when the animal leaps)
    public RPage CapLie = null!;                                 // the snow settled along a lying back
    public RPage Stand => Settle[ReindeerArt.SettlePages - 1];
    public RPage Lie => Down[ReindeerArt.LiePages - 1];
}

/// <summary>How the body is posed on one page. Feet are (near hind, near fore, far hind, far fore): x from the foot's own rest place under its hip, y up off the ground.</summary>
internal sealed class Pose
{
    public PointF[] Feet = ReindeerArt.SquareFeet();
    public float WD, CD;          // withers and croup dropped (body heights)
    public float Lift;            // the whole torso up (the trot's moment in the air)
    public float Rot, Sx;         // the torso turned about its middle (radians) and shifted: the shake
    public int Tail;              // 0 down, 1 up
    public float FoldF, FoldH;    // 0 standing to 1 lying: the forelegs fold, then the hind legs
}

internal static class ReindeerArt
{
    public const int WalkPages = 24, TrotPages = 16, SettlePages = 8, PawPages = 16, LiePages = 12, ShakePages = 9, GamPages = 6, TrotInPages = 6;
    public const int HeadPages = 16, HeadKinds = 3;   // head kinds: 0 jaw closed, 1 jaw open (chewing), 2 near ear flicked back
    // THE TURN is a half turn drawn in TurnSteps steps of 9 degrees (page 0 is the side view facing one way, page TurnSteps the side view
    // facing the other, the three-quarter view toward us and the head-on view between). The head pitch is one of TurnLevels, each EXACTLY
    // one of the ordinary head pages (TurnHp), so the head of the standing animal and the head of the first turn page are the same picture.
    // (Second pass: ONE level, head page 4, a head carried up and level. The herd brings the head up BEFORE it turns and holds it there through
    // the turn, so seven levels were seven times the paint for nothing, and the build fell from six seconds to under two.)
    public const int TurnSteps = 20, TurnLevels = 1;
    public static readonly int[] TurnHp = [4];
    public static float TurnF(int level) => TurnHp[level] / (float)(HeadPages - 1);
    public static int TurnLevelOf(float f)
    {
        int best = 0; float bd = 9f;
        for (int k = 0; k < TurnLevels; k++) { float d = MathF.Abs(TurnF(k) - f); if (d < bd) { bd = d; best = k; } }
        return best;
    }

    // ---- the gaits: how far a foot travels either side of its hip, and the share of the stride it is on the ground ----
    public const float WalkReach = 0.25f, WalkDuty = 0.62f;
    public const float TrotReach = 0.26f, TrotDuty = 0.42f;
    public const float PawReach = 0.26f;                    // how far in front of the shoulder the scraping hoof lands
    public const float NeckX = 0.40f, NeckY = 0.86f;        // where the neck leaves the body, standing

    // ---- colours: a grey-brown coat with a pale throat, rump, belly and lower legs, a bone-pale rack ----
    private static readonly Color Coat = Color.FromArgb(136, 120, 108), CoatDark = Color.FromArgb(98, 86, 78), CoatFar = Color.FromArgb(84, 74, 68);
    private static readonly Color CoatCalf = Color.FromArgb(154, 124, 102), CoatCalfDark = Color.FromArgb(116, 90, 72), CoatCalfFar = Color.FromArgb(98, 76, 62);
    private static readonly Color Cream = Color.FromArgb(232, 224, 206), Mane = Color.FromArgb(240, 234, 220);
    private static readonly Color LegPale = Color.FromArgb(190, 180, 164), LegFar = Color.FromArgb(142, 133, 120);
    private static readonly Color Hoof = Color.FromArgb(44, 36, 34), Nose = Color.FromArgb(34, 28, 30), White = Color.FromArgb(242, 238, 226);
    private static readonly Color Rim = Color.FromArgb(150, 196, 214, 246);     // the moon's light along the back
    private static readonly Color Antler = Color.FromArgb(218, 202, 174), AntlerFar = Color.FromArgb(160, 146, 126);
    private static readonly Color EarIn = Color.FromArgb(186, 140, 126), EarOut = Color.FromArgb(104, 90, 80);

    private static PointF P(float x, float y) => new(x, y);
    private static float Smooth(float x) { x = Math.Clamp(x, 0f, 1f); return x * x * (3 - 2 * x); }
    private static PointF Lerp(PointF a, PointF b, float t) => new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);

    public static PointF[] SquareFeet() => [P(-0.05f, 0), P(0.05f, 0), P(-0.10f, 0), P(0.11f, 0)];

    // ================================================================ the feet
    private static readonly float[] WalkOffsets = [0f, 0.75f, 0.5f, 0.25f];   // lateral sequence: near hind lands at 0, near fore at 0.25, far hind at 0.5, far fore at 0.75

    /// <summary>Where each foot is at phase p of the walking stride (0 to 1).</summary>
    public static PointF[] WalkFeet(float p)
    {
        var feet = new PointF[4];
        for (int j = 0; j < 4; j++)
        {
            float ph = (p + WalkOffsets[j]) % 1f, dx, up = 0;
            if (ph < WalkDuty) dx = WalkReach * (1 - 2 * ph / WalkDuty);          // on the ground: the foot slides back at exactly the body's speed
            else
            {
                float q = (ph - WalkDuty) / (1 - WalkDuty);
                dx = WalkReach * (-1 + 2 * q);                                    // in the air: it swings forward again, a high step
                up = 0.09f * MathF.Sin(MathF.PI * q);
            }
            feet[j] = P(dx + SquareFeet()[j].X * 0.4f, up);
        }
        return feet;
    }

    /// <summary>The trot: diagonal pairs (near hind with far fore, far hind with near fore), each pair on the ground 0.42 of the stride, so there is a moment in the air between pairs; the torso rises then.</summary>
    public static (PointF[] Feet, float Lift) TrotFeet(float p)
    {
        var feet = new PointF[4];
        float[] off = [0f, 0.5f, 0.5f, 0f];                   // near hind, near fore, far hind, far fore
        for (int j = 0; j < 4; j++)
        {
            float ph = (p + off[j]) % 1f, dx, up = 0;
            if (ph < TrotDuty) dx = TrotReach * (1 - 2 * ph / TrotDuty);
            else
            {
                float q = (ph - TrotDuty) / (1 - TrotDuty);
                dx = TrotReach * (-1 + 2 * q);
                up = 0.13f * MathF.Sin(MathF.PI * q);
            }
            feet[j] = P(dx + SquareFeet()[j].X * 0.4f, up);
        }
        float half = (p * 2) % 1f;                             // 0 to 1 over each half stride, two flights a stride
        float lift = half > TrotDuty ? 0.045f * MathF.Sin(MathF.PI * (half - TrotDuty) / (1 - TrotDuty)) : -0.012f * MathF.Sin(MathF.PI * half / TrotDuty);
        return (feet, lift);
    }

    /// <summary>The scraping foreleg at phase p of one scrape: lifted and reached forward (0 to 0.45), set down, then dragged back along the snow. The shoulder dips as the hoof strikes.</summary>
    public static (PointF[] Feet, float Dip) PawFeet(float p)
    {
        var feet = SquareFeet();
        float x, y;
        if (p < 0.45f)
        {
            float q = p / 0.45f;
            x = 0.05f + (PawReach - 0.05f) * Smooth(q);
            y = 0.24f * MathF.Sin(MathF.PI * q);
        }
        else
        {
            float q = (p - 0.45f) / 0.55f;
            x = PawReach - (PawReach - 0.05f) * Smooth(q);
            y = 0.03f * MathF.Sin(MathF.PI * q);
        }
        feet[1] = P(x, y);
        return (feet, 0.055f * MathF.Sin(MathF.PI * p));
    }

    // ================================================================ building a whole set

    /// <summary>Where the last Build's time went (for the mind log): bodies, heads, turn pages, the rest, in milliseconds.</summary>
    public static string Timing = "";

    public static ReindeerSet Build(int h, ReindeerKind kind)
    {
        var swT = System.Diagnostics.Stopwatch.StartNew();
        var set = new ReindeerSet { Kind = kind, H = h };
        bool calf = kind == ReindeerKind.Calf;
        float antlerH = kind == ReindeerKind.Bull ? 1.0f : kind == ReindeerKind.Cow ? 0.58f : 0f;
        set.BodyOx = (int)(0.82f * h) + 3;
        set.BodyW = (int)(1.66f * h) + 6;
        set.BodyOy = (int)(1.14f * h) + 3;
        set.BodyH = set.BodyOy + 4;
        set.HeadOx = (int)(0.80f * h) + 3;
        set.HeadW = (int)(1.9f * h) + 6;
        set.HeadOy = (int)((0.62f + 0.95f * antlerH) * h) + 6;
        set.HeadH = set.HeadOy + (int)(0.95f * h) + 4;

        RPage Body(Pose pose)
        {
            Nap();
            var page = new RPage();
            PointF neck = P(0, 0);
            // PaintCrop paints on one scratch bitmap kept for the whole build and hands back only the rows and columns that hold paint
            // (a stamp costs its painted rows, and a smaller sheet is also less to hold and to copy).
            page.S = PaintCrop(set.BodyW, set.BodyH, set.BodyOx, set.BodyOy, g =>
            {
                g.TranslateTransform(set.BodyOx, set.BodyOy);
                g.ScaleTransform(h, -h);                       // body heights, y UP
                neck = PaintBody(g, pose, kind, h);
            }, out page.OX, out page.OY);
            page.NX = neck.X; page.NY = neck.Y;
            return page;
        }
        RPage HeadPage(float sx, Action<Graphics> paint)
        {
            Nap();
            var page = new RPage();
            page.S = PaintCrop(set.HeadW, set.HeadH, set.HeadOx, set.HeadOy, g =>
            {
                g.TranslateTransform(set.HeadOx, set.HeadOy);
                g.ScaleTransform(h * sx, -h);
                paint(g);
            }, out page.OX, out page.OY);
            return page;
        }

        for (int i = 0; i < WalkPages; i++)
        {
            float p = i / (float)WalkPages;
            set.Walk[i] = Body(new Pose { Feet = WalkFeet(p), WD = WithersDip(p), CD = CroupDip(p) });
        }
        for (int i = 0; i < TrotPages; i++)
        {
            float p = i / (float)TrotPages;
            var (feet, lift) = TrotFeet(p);
            set.Trot[i] = Body(new Pose { Feet = feet, Lift = lift, WD = -0.01f, CD = 0.0f, Tail = 1 });
        }
        // The trot's start and stop: the square stance to the trot's first page in six pages (a foot at a time would be too slow for a
        // startle), so a trot never begins or ends by jumping from the walk's rest page to a trot page.
        {
            var (t0, l0) = TrotFeet(0f);
            for (int i = 0; i < TrotInPages; i++)
            {
                float k = (i + 1) / (float)TrotInPages;
                var feet = new PointF[4];
                for (int j = 0; j < 4; j++)
                {
                    PointF sq0 = SquareFeet()[j];
                    feet[j] = P(sq0.X + (t0[j].X - sq0.X) * k, t0[j].Y * k + 0.06f * MathF.Sin(MathF.PI * k) * (j % 2 == 0 ? 1f : 0.6f));
                }
                set.TrotIn[i] = Body(new Pose { Feet = feet, Lift = l0 * k, WD = -0.01f * k, Tail = k > 0.5f ? 1 : 0 });
            }
        }
        // The settle pages: the feet go from the walk's rest page to the square stance ONE AT A TIME, each lifting as it moves,
        // two pages a foot (blending all four along the ground slid a planted hoof 17 px in the deer).
        PointF[] rest = WalkFeet(0f), square = SquareFeet();
        int[] order = [3, 2, 1, 0];
        for (int i = 0; i < SettlePages; i++)
        {
            float k = (i + 1) / (float)SettlePages;
            var feet = new PointF[4];
            for (int n = 0; n < 4; n++)
            {
                int j = order[n];
                float kj = Math.Clamp((i + 1 - 2 * n) / 2f, 0f, 1f);
                feet[j] = P(rest[j].X + (square[j].X - rest[j].X) * kj, rest[j].Y * (1 - kj) + 0.05f * MathF.Sin(MathF.PI * kj));
            }
            float dip = 0.010f * (1 - k);
            set.Settle[i] = Body(new Pose { Feet = feet, WD = dip, CD = dip });
        }
        for (int i = 0; i < PawPages; i++)
        {
            var (feet, dip) = PawFeet(i / (float)PawPages);
            set.Paw[i] = Body(new Pose { Feet = feet, WD = dip });
        }
        // Lying down: the forelegs fold first (0 to 0.55 of the pages), the hind legs follow (0.35 to 1). Getting up the other
        // way about: the hind end pushes up first (the rump rises while the front is still kneeling), then the forelegs unfold.
        for (int i = 0; i < LiePages; i++)
        {
            float t = i / (float)(LiePages - 1);
            float ff = Smooth(t / 0.55f), hf = Smooth((t - 0.35f) / 0.65f);
            set.Down[i] = Body(new Pose { WD = 0.40f * ff, CD = 0.40f * hf, FoldF = ff, FoldH = hf });
            float ut = 1 - t;                                   // the same pages walked backward, but with the hind end leading
            float hu = Smooth(ut / 0.55f), fu = Smooth((ut - 0.35f) / 0.65f);
            set.Up[LiePages - 1 - i] = Body(new Pose { WD = 0.40f * fu, CD = 0.40f * hu, FoldF = fu, FoldH = hu });
        }
        for (int i = 0; i < ShakePages; i++)
        {
            float v = i / (float)(ShakePages - 1) * 2 - 1;     // -1 to +1
            set.Shake[i] = Body(new Pose { Rot = v * 0.065f, Sx = v * 0.035f, WD = 0.01f * MathF.Abs(v), CD = 0.01f * MathF.Abs(v) });
        }
        if (calf)
        {
            // The calf's play (as the fawn's, Thanksgiving DeerHerd): load, gather (all four legs stiff under a hunched back),
            // reach (hind legs back, forelegs forward, airborne), land, skid, and TUCK (the legs drawn up for its try at flying).
            PointF[] Gf(float hind, float fore, float up) => [P(hind, up), P(fore, up), P(hind - 0.05f, up), P(fore + 0.05f, up)];
            (PointF[] feet, float wd, float cd, int tail)[] gam =
            [
                (SquareFeet(), 0.05f, 0.055f, 0),
                (Gf(0.05f, -0.02f, 0.05f), 0.04f, 0.045f, 1),
                (Gf(-0.20f, 0.20f, 0.10f), 0f, 0f, 1),
                (Gf(-0.04f, 0.12f, 0.0f), 0.02f, 0.02f, 1),
                (Gf(0.05f, 0.14f, 0.0f), 0f, 0.06f, 1),
                (Gf(0.10f, 0.04f, 0.26f), -0.01f, 0.0f, 1),
            ];
            for (int t = 0; t < GamPages; t++)
            {
                var (gf, gwd, gcd, gt) = gam[t];
                set.Gam[t] = Body(new Pose { Feet = gf, WD = gwd, CD = gcd, Tail = gt });
            }
        }
        long msBodies = swT.ElapsedMilliseconds;
        for (int j = 0; j < HeadPages; j++)
        {
            float f = j / (float)(HeadPages - 1);
            for (int kd = 0; kd < HeadKinds; kd++)
            {
                int k2 = kd;
                set.Head[j, kd] = HeadPage(1f, g => PaintHead(g, f, k2, kind, h));
            }
            set.Nose[j] = NoseTip(f, kind);
        }
        long msHeads = swT.ElapsedMilliseconds;
        // THE TURN: whole animals drawn at every angle of the half turn, toward us (side 0) and away from us (side 1), at each head pitch level.
        set.FigW = (int)(3.5f * h) + 10; set.FigOx = set.FigW / 2;
        set.FigOy = (int)((0.86f + 0.62f + 0.95f * antlerH) * h) + 10;
        set.FigH = set.FigOy + (int)(0.12f * h) + 6;
        for (int side = 0; side < 2; side++)
            for (int lv = 0; lv < TurnLevels; lv++)
                for (int k = 1; k < TurnSteps; k++)
                {
                    Nap();
                    var page = new RPage();
                    float f = TurnF(lv), phi = k * 180f / TurnSteps; int sd = side == 0 ? 1 : -1;
                    page.S = PaintCrop(set.FigW, set.FigH, set.FigOx, set.FigOy, g =>
                    {
                        g.TranslateTransform(set.FigOx, set.FigOy);
                        g.ScaleTransform(h, -h);
                        PaintFigure(g, kind, h, phi, sd, f);
                    }, out page.OX, out page.OY);
                    set.Fig[side, lv, k] = page;
                }
        long msTurn = swT.ElapsedMilliseconds;
        Timing = $"{kind}: bodies {msBodies} ms, heads {msHeads - msBodies} ms, turn pages {msTurn - msHeads} ms";
        // The shadows: a flat soft ellipse on the snow, blue because it is night and the snow is lit by the moon and the sky.
        set.Shadow = ShadowSprite(h, 1.30f);
        set.ShadowLie = ShadowSprite(h, 1.55f);
        // The snow that settles along a lying back (drawn over the lying page with a rising opacity).
        set.CapLie = new RPage();
        set.CapLie.S = PaintCrop(set.BodyW, set.BodyH, set.BodyOx, set.BodyOy, g =>
        {
            g.TranslateTransform(set.BodyOx, set.BodyOy);
            g.ScaleTransform(h, -h);
            PaintCap(g, new Pose { WD = 0.40f, CD = 0.40f, FoldF = 1, FoldH = 1 }, kind);
        }, out set.CapLie.OX, out set.CapLie.OY);
        return set;
    }

    private static int _naps;
    /// <summary>Every fourth page the painter sleeps a moment so the build thread leaves a core to the frames being drawn (a test run of the whole herd's build took frames from 6 ms to 60 ms in the first seconds).</summary>
    private static void Nap() { if ((Interlocked.Increment(ref _naps) & 3) == 0) Thread.Sleep(1); }

    // A brush per colour, made once and kept for the whole build (a fresh brush for every polygon was a quarter of the build's garbage).
    [ThreadStatic] private static Dictionary<int, SolidBrush>? _brushes;
    private static SolidBrush Br(Color c)
    {
        var d = _brushes ??= new Dictionary<int, SolidBrush>();
        int k = c.ToArgb();
        if (!d.TryGetValue(k, out var b)) d[k] = b = new SolidBrush(c);
        return b;
    }

    // ---- the scratch painter: ONE bitmap and ONE Graphics for the whole build (a fresh sheet per page was most of the garbage) ----
    [ThreadStatic] private static Bitmap? _tb;
    [ThreadStatic] private static Graphics? _tg;
    [ThreadStatic] private static int[]? _traw;
    [ThreadStatic] private static int _tw, _th;

    /// <summary>Frees the scratch bitmap; the build calls it when its last page is painted.</summary>
    public static void ReleaseScratch()
    {
        if (_brushes != null) { foreach (var b in _brushes.Values) b.Dispose(); _brushes = null; }
        _tg?.Dispose(); _tb?.Dispose(); _tg = null; _tb = null; _traw = null; _tw = _th = 0;
    }

    /// <summary>
    /// Paints one page on the scratch bitmap and returns only the rows and columns that hold paint, as a sprite, plus where the
    /// sheet's origin (ox, oy) is inside it. Same pixels as Sprite.Paint followed by a crop, with no whole-sheet arrays left over.
    /// </summary>
    private static Sprite PaintCrop(int w, int h, int ox, int oy, Action<Graphics> paint, out int nox, out int noy)
    {
        w = Math.Max(1, w); h = Math.Max(1, h);
        if (_tb == null || _tw < w || _th < h)
        {
            int nw = Math.Max(w, _tw), nh = Math.Max(h, _th);
            _tg?.Dispose(); _tb?.Dispose();
            _tb = new Bitmap(nw, nh, PixelFormat.Format32bppArgb);
            _tg = Graphics.FromImage(_tb);
            _tg.SmoothingMode = SmoothingMode.AntiAlias;
            _tw = nw; _th = nh;
            _traw = new int[nw * nh];
        }
        Graphics g = _tg!;
        g.ResetTransform(); g.ResetClip();
        g.SetClip(new Rectangle(0, 0, w, h));
        g.Clear(Color.Transparent);
        paint(g);
        g.ResetTransform(); g.ResetClip();
        int[] raw = _traw!;
        var data = _tb.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try { for (int y = 0; y < h; y++) System.Runtime.InteropServices.Marshal.Copy(data.Scan0 + y * data.Stride, raw, y * w, w); }
        finally { _tb.UnlockBits(data); }
        int x0 = w, y0 = h, x1 = -1, y1 = -1;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                if (((uint)raw[y * w + x] >> 24) != 0) { if (x < x0) x0 = x; if (x > x1) x1 = x; if (y < y0) y0 = y; if (y > y1) y1 = y; }
        if (x1 < 0) { nox = 0; noy = 0; return Sprite.FromPixels(1, 1, [0u]); }
        int cw = x1 - x0 + 1, ch = y1 - y0 + 1;
        var px = new uint[cw * ch];
        for (int y = 0; y < ch; y++)
            System.Runtime.InteropServices.MemoryMarshal.Cast<int, uint>(raw.AsSpan((y0 + y) * w + x0, cw)).CopyTo(px.AsSpan(y * cw, cw));
        nox = ox - x0; noy = oy - y0;
        return Sprite.FromPixels(cw, ch, px);
    }

    private static Sprite ShadowSprite(int h, float widthH)
    {
        int w = (int)(widthH * h) + 6, hh = Math.Max(4, (int)(0.17f * h) + 3);
        return Sprite.Paint(w, hh, g =>
        {
            using var b1 = new SolidBrush(Color.FromArgb(34, 30, 48, 120));
            using var b2 = new SolidBrush(Color.FromArgb(40, 30, 48, 120));
            using var b3 = new SolidBrush(Color.FromArgb(40, 30, 48, 120));
            g.FillEllipse(b1, 0.5f, 0.5f, w - 1f, hh - 1f);
            g.FillEllipse(b2, w * 0.12f, hh * 0.2f, w * 0.76f, hh * 0.6f);
            g.FillEllipse(b3, w * 0.28f, hh * 0.32f, w * 0.44f, hh * 0.36f);
        });
    }

    /// <summary>The withers dip as each forefoot lands (at 0.25 and 0.75 of the stride); positive is DOWN, in body heights.</summary>
    public static float WithersDip(float p) => 0.014f * (0.5f + 0.5f * MathF.Cos(4 * MathF.PI * (p - 0.25f)));
    public static float CroupDip(float p) => 0.014f * (0.5f + 0.5f * MathF.Cos(4 * MathF.PI * p));

    // ================================================================ the body

    /// <summary>Maps a point of the standing body to where it is on this pose (the torso sinks at the front and the back, tips and shifts for the shake; a calf is a shorter body on longer legs).</summary>
    private readonly struct Warp
    {
        private readonly float _wd, _cd, _lift, _cos, _sin, _sx;
        private readonly bool _calf;
        public Warp(Pose p, bool calf)
        {
            _wd = p.WD; _cd = p.CD; _lift = p.Lift; _cos = MathF.Cos(p.Rot); _sin = MathF.Sin(p.Rot); _sx = p.Sx; _calf = calf;
        }
        public PointF Q(float x, float y)
        {
            float s = Math.Clamp((x + 0.65f) / 1.2f, 0f, 1f); s = s * s * (3 - 2 * s);
            float dy = _cd * (1 - s) + _wd * s;                      // the croup's drop at the back, the withers' at the front
            if (_calf) { x *= 0.88f; y += (1 - y) * 0.10f; }
            y = y - dy + _lift;
            float rx = x * _cos - (y - 0.6f) * _sin, ry = x * _sin + (y - 0.6f) * _cos + 0.6f;
            return new PointF(rx + _sx, ry);
        }
    }

    private static readonly float[] HipRest = [-0.42f, 0.30f, -0.46f, 0.27f];   // near hind, near fore, far hind, far fore

    /// <summary>Two bones of equal length from a to b: the joint, on the LEFT of the way from a to b (so forward when the foot is below).</summary>
    private static PointF Ik(PointF a, PointF b, float bone)
    {
        float dx = b.X - a.X, dy = b.Y - a.Y;
        float d = MathF.Min(MathF.Sqrt(dx * dx + dy * dy), 2 * bone - 0.002f);
        float alpha = MathF.Acos(Math.Clamp(d / (2 * bone), -1f, 1f));
        float th = MathF.Atan2(dy, dx) + alpha;
        return P(a.X + bone * MathF.Cos(th), a.Y + bone * MathF.Sin(th));
    }

    /// <summary>Paints the body and legs; returns where the neck's root is on this pose.</summary>
    private static PointF PaintBody(Graphics g, Pose pose, ReindeerKind kind, int h)
    {
        bool calf = kind == ReindeerKind.Calf;
        float pw = MathF.Max(0.012f, 1.0f / h);                 // one pixel, in body heights
        Color coat = calf ? CoatCalf : Coat, dark = calf ? CoatCalfDark : CoatDark, far = calf ? CoatCalfFar : CoatFar;
        var w = new Warp(pose, calf);
        PointF[] Pts(params float[] v)
        {
            var r = new PointF[v.Length / 2];
            for (int i = 0; i < r.Length; i++) r[i] = w.Q(v[2 * i], v[2 * i + 1]);
            return r;
        }
        void Blob(PointF[] pts, Color c, float tension = 0.5f)
        {
            using var path = new GraphicsPath();
            path.AddClosedCurve(pts, tension);
            var b = Br(c);
            g.FillPath(b, path);
        }
        void Bone(PointF a, PointF b, float wa, float wb, Color c)
        {
            wa = MathF.Max(wa, 1.6f * pw); wb = MathF.Max(wb, 1.3f * pw);
            float dx = b.X - a.X, dy = b.Y - a.Y, l = MathF.Sqrt(dx * dx + dy * dy) + 1e-5f, nx = -dy / l, ny = dx / l;
            var br = Br(c);
            g.FillPolygon(br, [P(a.X + nx * wa / 2, a.Y + ny * wa / 2), P(b.X + nx * wb / 2, b.Y + ny * wb / 2), P(b.X - nx * wb / 2, b.Y - ny * wb / 2), P(a.X - nx * wa / 2, a.Y - ny * wa / 2)]);
            g.FillEllipse(br, a.X - wa / 2, a.Y - wa / 2, wa, wa);
            g.FillEllipse(br, b.X - wb / 2, b.Y - wb / 2, wb, wb);
        }
        void HoofAt(PointF f)
        {
            var hb = Br(Hoof);
            g.FillPolygon(hb, [P(f.X - 0.035f, f.Y + 0.032f), P(f.X + 0.045f, f.Y + 0.028f), P(f.X + 0.058f, f.Y - 0.022f), P(f.X - 0.040f, f.Y - 0.022f)]);
        }

        float ls = calf ? 1.07f : 1f;                            // the calf's legs are long for its body
        void Leg(int j)
        {
            bool hind = j == 0 || j == 2, isFar = j >= 2;
            float rest = HipRest[j] * (calf ? 0.88f : 1f);
            PointF hip = w.Q(HipRest[j], hind ? 0.64f : 0.58f);
            PointF foot = P(rest + pose.Feet[j].X, 0.02f + pose.Feet[j].Y);
            Color upper = isFar ? far : coat, lower = isFar ? LegFar : LegPale;
            float fold = hind ? pose.FoldH : pose.FoldF;
            if (!hind)
            {
                PointF knee = Ik(hip, foot, 0.29f * ls);
                if (fold > 0)
                {
                    float k = Smooth(fold);                      // kneeling: the knee goes forward onto the snow, the cannon folds back under it
                    knee = Lerp(knee, P(hip.X + 0.20f, 0.045f), k);
                    foot = Lerp(foot, P(hip.X - 0.02f, 0.035f), k);
                }
                Bone(hip, knee, 0.125f, 0.082f, upper);
                Bone(knee, foot, 0.072f, 0.056f, lower);
            }
            else
            {
                float cannon = 0.25f * ls;
                float phi = Math.Clamp(0.10f + 1.3f * pose.Feet[j].X - 2.0f * pose.Feet[j].Y, -0.5f, 0.7f);   // the cannon leans forward as the foot reaches ahead and swings back as it lifts
                PointF hock = P(foot.X - cannon * MathF.Sin(phi), foot.Y + cannon * MathF.Cos(phi));
                PointF stifle = Ik(hip, hock, 0.27f * ls);       // the stifle forward, so the hock points BACK
                if (fold > 0)
                {
                    float k = Smooth(fold);                      // folded under the body: hock back on the snow, the cannon forward beneath the belly
                    stifle = Lerp(stifle, P(hip.X + 0.16f, 0.07f), k);
                    hock = Lerp(hock, P(hip.X - 0.22f, 0.045f), k);
                    foot = Lerp(foot, P(hip.X + 0.02f, 0.035f), k);
                }
                Bone(hip, stifle, 0.17f, 0.105f, upper);
                Bone(stifle, hock, 0.105f, 0.075f, upper);
                Bone(hock, foot, 0.072f, 0.056f, lower);
            }
            HoofAt(foot);
        }
        Leg(2); Leg(3);

        // The torso: a deep chest, a level back, a rounded rump; the darker saddle along the top; the cream belly wrapping up
        // into the flank; the pale rump patch; the shoulder and haunch as soft masses; the moonlit rim along the very top.
        Blob(Pts(0.34f, 0.99f, 0.52f, 0.88f, 0.58f, 0.68f, 0.46f, 0.50f, 0.20f, 0.45f, -0.10f, 0.46f, -0.38f, 0.52f, -0.62f, 0.58f, -0.72f, 0.76f, -0.62f, 0.91f, -0.34f, 0.96f, 0.0f, 0.97f), coat);
        Blob(Pts(0.30f, 0.975f, 0.0f, 0.955f, -0.34f, 0.945f, -0.60f, 0.90f, -0.50f, 0.83f, -0.20f, 0.87f, 0.10f, 0.88f, 0.34f, 0.88f), dark, 0.4f);
        Blob(Pts(0.50f, 0.62f, 0.44f, 0.50f, 0.20f, 0.455f, -0.12f, 0.465f, -0.40f, 0.53f, -0.50f, 0.60f, -0.30f, 0.56f, -0.05f, 0.53f, 0.22f, 0.54f, 0.40f, 0.60f), Cream, 0.45f);
        Blob(Pts(-0.50f, 0.86f, -0.64f, 0.80f, -0.64f, 0.66f, -0.54f, 0.62f, -0.46f, 0.74f), Color.FromArgb(215, Cream), 0.4f);
        using (var mass = new SolidBrush(Color.FromArgb(60, dark)))
        {
            Blob(Pts(-0.58f, 0.78f, -0.40f, 0.82f, -0.30f, 0.64f, -0.46f, 0.52f, -0.62f, 0.60f), Color.FromArgb(60, dark));   // the haunch
            Blob(Pts(0.10f, 0.74f, 0.34f, 0.80f, 0.46f, 0.64f, 0.34f, 0.50f, 0.14f, 0.56f), Color.FromArgb(55, dark));        // the shoulder
        }
        {
            float tk = MathF.Max(0.022f, 1.5f * pw);
            Blob(Pts(0.34f, 0.995f, 0.0f, 0.975f, -0.34f, 0.965f, -0.62f, 0.925f, -0.34f, 0.965f - tk, 0.0f, 0.975f - tk, 0.34f, 0.995f - tk), Rim, 0.3f);
        }
        // The tail: a short white tuft at the rump, down at rest, up when the animal is alert or running.
        {
            PointF root = w.Q(-0.68f, 0.83f);
            float ang = pose.Tail == 0 ? 3.55f : 2.5f, len = 0.10f;
            PointF tip = P(root.X + len * MathF.Cos(ang), root.Y + len * MathF.Sin(ang));
            float tx = -(tip.Y - root.Y), ty = tip.X - root.X;
            Blob([root, P(root.X + tx * 0.30f, root.Y + ty * 0.30f), P(tip.X + tx * 0.2f, tip.Y + ty * 0.2f), tip, P(tip.X - tx * 0.2f, tip.Y - ty * 0.2f), P(root.X - tx * 0.30f, root.Y - ty * 0.30f)], White, 0.4f);
        }
        Leg(0); Leg(1);
        return w.Q(NeckX, NeckY);
    }

    /// <summary>The snow that settles along a lying back: a lumpy cap on the topline, thicker over the loin.</summary>
    private static void PaintCap(Graphics g, Pose pose, ReindeerKind kind)
    {
        var w = new Warp(pose, kind == ReindeerKind.Calf);
        float[] xs = [0.40f, 0.26f, 0.10f, -0.06f, -0.22f, -0.38f, -0.52f, -0.64f];
        float[] top = [0.985f, 0.978f, 0.97f, 0.965f, 0.96f, 0.953f, 0.935f, 0.89f];
        float[] th = [0.025f, 0.06f, 0.08f, 0.09f, 0.085f, 0.075f, 0.05f, 0.02f];
        var pts = new List<PointF>();
        for (int i = 0; i < xs.Length; i++) pts.Add(w.Q(xs[i], top[i] + 0.012f));
        for (int i = xs.Length - 1; i >= 0; i--) pts.Add(w.Q(xs[i], top[i] - th[i]));
        using var path = new GraphicsPath();
        path.AddClosedCurve(pts.ToArray(), 0.35f);
        using var b = new SolidBrush(Color.FromArgb(246, 244, 250, 255));
        g.FillPath(b, path);
        var pts2 = new List<PointF>();
        for (int i = 0; i < xs.Length; i++) pts2.Add(w.Q(xs[i], top[i] - th[i] * 0.55f));
        for (int i = xs.Length - 1; i >= 0; i--) pts2.Add(w.Q(xs[i], top[i] - th[i]));
        using var path2 = new GraphicsPath();
        path2.AddClosedCurve(pts2.ToArray(), 0.35f);
        using var b2 = new SolidBrush(Color.FromArgb(120, 190, 206, 236));
        g.FillPath(b2, path2);                                   // the shaded underside of the drift
    }

    // ================================================================ the head

    private static float NeckAngle(float f) => (6f + 118f * f) * MathF.PI / 180f;          // from straight up, forward
    private static float HeadAxis(float f) => (50f + 125f * f) * MathF.PI / 180f;          // 50 degrees is nose up at the sky, 175 is nose to the snow
    private static float NeckLen(float f, ReindeerKind k) => (k == ReindeerKind.Calf ? 0.36f : 0.41f) + 0.10f * f;
    private static float MuzzleLen(ReindeerKind k) => k == ReindeerKind.Bull ? 0.35f : k == ReindeerKind.Cow ? 0.33f : 0.25f;
    private static PointF Along(PointF from, float ang, float d) => P(from.X + d * MathF.Sin(ang), from.Y + d * MathF.Cos(ang));

    /// <summary>The nostril on the head page for pitch f, in body heights from the neck's root (y up).</summary>
    public static PointF NoseTip(float f, ReindeerKind k)
    {
        float a = NeckAngle(f), ha = HeadAxis(f), len = NeckLen(f, k);
        PointF top = P(len * MathF.Sin(a), len * MathF.Cos(a));
        PointF mid = Along(top, ha, 0.10f);
        return Along(mid, ha, MuzzleLen(k) + 0.02f);
    }

    /// <summary>
    /// The neck and head on a sheet whose origin is the neck's root. f: 0 = head up with the nose at the sky, 0.25 = alert and level,
    /// 1 = nose down in the snow. kind: 0 jaw closed, 1 jaw open (chewing), 2 the near ear flicked back.
    /// </summary>
    private static void PaintHead(Graphics g, float f, int kind, ReindeerKind rk, int h)
    {
        bool calf = rk == ReindeerKind.Calf, bull = rk == ReindeerKind.Bull;
        float pw = MathF.Max(0.012f, 1.0f / h);
        Color coat = calf ? CoatCalf : Coat, dark = calf ? CoatCalfDark : CoatDark;
        float a = NeckAngle(f), ha = HeadAxis(f), len = NeckLen(f, rk);
        PointF root = P(0, 0);
        PointF top = P(len * MathF.Sin(a), len * MathF.Cos(a));
        PointF headMid = Along(top, ha, 0.10f);
        float w0 = bull ? 0.46f : calf ? 0.32f : 0.38f, w1 = bull ? 0.31f : calf ? 0.22f : 0.27f;

        // The neck: a thick tapered shape, thick at the shoulders and slimmer under the head, the darker coat along its back
        // and the cream mane hanging from its front edge like a beard.
        {
            float nx = MathF.Cos(a), ny = -MathF.Sin(a);                   // across the neck, toward its front (the throat)
            using var b = new SolidBrush(coat);
            using var path = new GraphicsPath();
            path.AddClosedCurve([P(root.X + nx * w0 / 2, root.Y + ny * w0 / 2), P(top.X + nx * w1 / 2, top.Y + ny * w1 / 2), P(top.X - nx * w1 / 2, top.Y - ny * w1 / 2), P(root.X - nx * w0 / 2 - 0.05f, root.Y - ny * w0 / 2)], 0.25f);
            g.FillPath(b, path);
            using var back = new SolidBrush(Color.FromArgb(120, dark));
            using var bp = new GraphicsPath();
            PointF m0 = P(root.X - nx * w0 * 0.30f, root.Y - ny * w0 * 0.30f), m1 = P(top.X - nx * w1 * 0.30f, top.Y - ny * w1 * 0.30f);
            bp.AddClosedCurve([m0, m1, P(m1.X - nx * 0.05f, m1.Y - ny * 0.05f), P(m0.X - nx * 0.08f, m0.Y - ny * 0.08f)], 0.2f);
            g.FillPath(back, bp);
            // The mane: a fringe along the front edge with tufts hanging down (gravity, not along the neck).
            var fringe = new List<PointF>();
            int n = 7;
            for (int i = 0; i <= n; i++)
            {
                float t = i / (float)n, ww = w0 + (w1 - w0) * t;
                PointF at = P(root.X + (top.X - root.X) * t + nx * ww * 0.5f, root.Y + (top.Y - root.Y) * t + ny * ww * 0.5f);
                fringe.Add(at);
            }
            var shape = new List<PointF>();
            for (int i = 0; i < fringe.Count; i++)
            {
                shape.Add(P(fringe[i].X - nx * 0.10f * (bull ? 1.2f : 1f), fringe[i].Y - ny * 0.10f));
            }
            for (int i = fringe.Count - 1; i >= 0; i--)
            {
                float tuft = (i % 2 == 0 ? 0.10f : 0.045f) * (bull ? 1.25f : 1f) * (0.5f + 0.5f * (1 - i / (float)n));
                shape.Add(P(fringe[i].X + nx * 0.035f, fringe[i].Y + ny * 0.035f - tuft));
            }
            using var mp = new GraphicsPath();
            mp.AddClosedCurve(shape.ToArray(), 0.15f);
            using var mb = new SolidBrush(Mane);
            g.FillPath(mb, mp);
        }

        var st = g.Save();
        g.TranslateTransform(headMid.X, headMid.Y);
        g.RotateTransform(90f - ha * 180f / MathF.PI);
        float ml = MuzzleLen(rk);
        float sk = calf ? 0.20f : 0.17f;                                   // skull half length (a calf's head is big for its body)
        float sh = calf ? 0.135f : 0.115f;                                  // skull half height

        void EarAt(float x, float y, float deg, float length, Color c, Color inner)
        {
            var es = g.Save();
            g.TranslateTransform(x, y);
            g.RotateTransform(deg);
            using (var e1 = new SolidBrush(c)) g.FillEllipse(e1, -0.015f, -0.04f, length, 0.08f);
            using (var e2 = new SolidBrush(inner)) g.FillEllipse(e2, 0.01f, -0.022f, length * 0.62f, 0.044f);
            g.Restore(es);
        }
        float earBase = f < 0.18f ? 95f : 118f;                           // ears pricked up when the head is high, relaxed out and back otherwise
        EarAt(-0.11f, 0.075f, earBase + 28f, 0.15f, EarOut, Color.FromArgb(150, 110, 100));   // the far ear

        // The antlers (behind the skull so it covers their roots): the far one darker and a little smaller.
        if (rk != ReindeerKind.Calf)
        {
            float s = bull ? 1.0f : 0.58f;
            PaintAntler(g, s * 0.90f, AntlerFar, P(-0.14f, 0.11f), bull, pw);
            PaintAntler(g, s, Antler, P(-0.02f, 0.09f), bull, pw);
        }

        using (var skull = new SolidBrush(coat)) g.FillEllipse(skull, -sk, -sh, sk * 2, sh * 2);
        using (var muzzle = new SolidBrush(coat))
        using (var mp = new GraphicsPath())
        {
            // A long broad muzzle that squares off at the nose, the jaw curving under it.
            mp.AddClosedCurve([P(0.0f, 0.085f), P(ml * 0.55f, 0.072f), P(ml, 0.058f), P(ml + 0.035f, 0.0f), P(ml, -0.062f), P(ml * 0.55f, -0.088f), P(0.02f, -0.10f), P(-0.07f, -0.07f)], 0.4f);
            g.FillPath(muzzle, mp);
        }
        if (kind == 1)
        {
            using var jaw = new SolidBrush(dark);
            g.FillPolygon(jaw, [P(0.04f, -0.07f), P(ml - 0.02f, -0.085f), P(ml + 0.01f, -0.035f), P(ml - 0.04f, -0.04f)]);
        }
        using (var ring = new SolidBrush(Color.FromArgb(205, 190, 172, 156)))
            g.FillPolygon(ring, [P(ml - 0.09f, 0.040f), P(ml, 0.045f), P(ml + 0.035f, 0.0f), P(ml, -0.06f), P(ml - 0.09f, -0.07f)]);
        using (var nose = new SolidBrush(Nose)) g.FillEllipse(nose, ml - 0.035f, -0.045f, 0.085f, 0.075f);
        using (var eye = new SolidBrush(Nose)) g.FillEllipse(eye, 0.045f, 0.018f, MathF.Max(0.058f, 3.0f * pw), MathF.Max(0.058f, 3.0f * pw));
        using (var glint = new SolidBrush(Color.White)) g.FillEllipse(glint, 0.066f, 0.050f, MathF.Max(0.018f, 1.3f * pw), MathF.Max(0.018f, 1.3f * pw));
        EarAt(-0.09f, 0.085f, earBase + (kind == 2 ? 52f : 0f), 0.15f, coat, EarIn);        // the near ear, flicked back for kind 2
        g.Restore(st);
    }

    /// <summary>
    /// One antler in the head's own frame (x toward the nose, y up from the skull), from "base". The MAIN BEAM sweeps up and back
    /// and curves forward at the top; the brow shovel reaches forward over the face, a second tine forward, and a bull's rack
    /// has back tines and a palmate top. A cow's is only the beam, a brow tine and one more.
    /// </summary>
    private static void PaintAntler(Graphics g, float s, Color c, PointF at, bool bull, float pw)
    {
        float wBeam = MathF.Max(bull ? 0.050f : 0.034f, 2.3f * pw);
        PointF T(float x, float y) => P(at.X + x * s, at.Y + y * s);
        using var penB = new Pen(c, wBeam) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        using var penT = new Pen(c, MathF.Max(wBeam * 0.72f, 1.8f * pw)) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        using var penTip = new Pen(c, MathF.Max(wBeam * 0.5f, 1.5f * pw)) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        g.DrawCurve(penB, [T(0, 0), T(-0.10f, 0.20f), T(-0.20f, 0.42f), T(-0.19f, 0.62f)], 0.5f);
        g.DrawCurve(penT, [T(-0.19f, 0.62f), T(-0.06f, 0.80f), T(0.14f, 0.90f)], 0.5f);
        // Brow tine over the face: flat and broad at its end (the shovel).
        g.DrawCurve(penT, [T(0.02f, 0.07f), T(0.13f, 0.15f), T(0.24f, 0.14f)], 0.5f);
        using (var shovel = new SolidBrush(c))
        {
            using var sp = new GraphicsPath();
            sp.AddClosedCurve([T(0.18f, 0.17f), T(0.27f, 0.20f), T(0.33f, 0.13f), T(0.26f, 0.08f)], 0.4f);
            g.FillPath(shovel, sp);
        }
        g.DrawCurve(penT, [T(-0.17f, 0.36f), T(-0.04f, 0.47f), T(0.09f, 0.49f)], 0.5f);      // the second forward tine
        if (bull)
        {
            g.DrawCurve(penT, [T(-0.20f, 0.52f), T(-0.33f, 0.65f), T(-0.36f, 0.78f)], 0.5f);    // a tine off the back of the beam
            g.DrawCurve(penTip, [T(-0.10f, 0.76f), T(-0.13f, 0.92f), T(-0.09f, 1.04f)], 0.5f);   // one going straight up
            g.DrawCurve(penTip, [T(-0.24f, 0.30f), T(-0.36f, 0.40f), T(-0.40f, 0.52f)], 0.5f);   // a low back tine
            using var palm = new SolidBrush(c);
            using var pp = new GraphicsPath();
            pp.AddClosedCurve([T(0.08f, 0.84f), T(0.20f, 0.97f), T(0.32f, 0.92f), T(0.28f, 0.80f), T(0.16f, 0.78f)], 0.4f);
            g.FillPath(palm, pp);                                                             // the palmate top
        }
    }

    // ================================================================ THE TURN: whole animals drawn from every angle
    //
    // FEYNMAN VERSION. The old turn squeezed the standing picture narrower and narrower, so for a few frames a reindeer was a thin
    // tall card. A real animal turning on the spot shows a DIFFERENT PICTURE of itself at every angle: the side, then three-quarters
    // toward us (one eye, two forelegs, the near flank), then head-on (a chest, two forelegs side by side, the hind legs behind,
    // the antlers seen from the front), then three-quarters the other way, then the other side. So each page of the turn is painted
    // as a little 3D animal turned to a given angle and flattened to the screen, the way a shadow puppet of a model reindeer would be.
    //
    // HOW. Every part is a loft: a row of cross-sections (ovals) along the part, and each pair of neighbours is joined by the
    // smallest convex shape holding both (the "hull": a rubber band stretched round two coins). Turn the model by an angle and
    // every oval's centre slides across the screen by x * cos(angle) and every oval's width grows by its half-thickness * sin(angle).
    // The torso is 23 ovals along the back, the neck 7, the skull 7, the muzzle 5. The legs are bones with real knees: the joint
    // of two equal bones is found in 3D, bent toward the animal's forward direction, so a foreleg seen head-on is straight and a
    // hind leg still zigzags. Antlers are curves in 3D (the beam goes up AND out sideways, so head-on the rack is a wide V).
    // Parts are painted far to near by their depth, so head-on the hind legs are behind the belly and the forelegs in front of it.
    //
    // THE ANGLE. Phi runs 0 to 180 degrees over the TurnSteps pages. "side 0" turns toward us (the face comes round to us, then past
    // us), "side 1" turns away (we see the rump, the pale patch and the short tail). The head LEADS the body by up to 17 degrees
    // (psi = phi + 0.3 sin phi) and the neck twists between them. The page for phi = 0 and for phi = 180 are NOT drawn here: the
    // ordinary standing pages are used there, so the turn has no pop at either end. Turning to the right is the same pages
    // stamped mirrored, as every other page of the herd is.
    //
    // THE FEET. A turn on the spot is a pivot on planted feet. Each foot stays where it last landed (so it never slides) and the
    // body swings round it; when the foot has fallen about 25 degrees behind the body it lifts (a 0.10 body-height hop), swings
    // and lands 26 degrees AHEAD of the body, the legs going in the order near hind, far fore, near fore, far hind. Three steps a
    // foot, twelve in all, one at a time: a pivot, never a skate. The last step of each foot lands exactly on the final square
    // stance, and the first step starts after page 1, so the feet are continuous with the standing pages at both ends.

    private const int StepEvents = 12;
    private const float StepT0 = 17f, StepDt = 13.27f, StepHalf = 7f, StepLead = 26f;     // in degrees of body yaw
    private static readonly int[] StepOrder = [0, 3, 1, 2];                              // near hind, far fore, near fore, far hind
    private static readonly float[] LegZ = [0.13f, 0.12f, -0.13f, -0.12f];               // each leg's sideways place under the body (near legs +, toward us)

    /// <summary>Where foot j is at body yaw phiDeg: world x, the lift off the snow, and the depth (toward us is +).</summary>
    private static (float X, float Lift, float Z) FootWorld(int j, float phiDeg, int s, bool calf)
    {
        float xr = HipRest[j] * (calf ? 0.88f : 1f) + SquareFeet()[j].X, zr = LegZ[j];
        float thFrom = 0f, thTo = 0f, u = -1f;
        for (int e = 0; e < StepEvents; e++)
        {
            if (StepOrder[e % 4] != j) continue;
            float t = StepT0 + StepDt * e, begin = t - StepHalf, land = t + StepHalf;
            float lands = e >= StepEvents - 4 ? 180f : MathF.Min(180f, land + StepLead);
            if (phiDeg >= land) { thFrom = lands; continue; }
            if (phiDeg > begin) { thTo = lands; u = (phiDeg - begin) / (2 * StepHalf); }
            break;
        }
        static (float X, float Z) At(float xr, float zr, float thDeg, int s)
        {
            float th = thDeg * MathF.PI / 180f, c = MathF.Cos(th), sn = MathF.Sin(th);
            return (xr * c - s * zr * sn, s * xr * sn + zr * c);
        }
        var p0 = At(xr, zr, thFrom, s);
        if (u < 0) return (p0.X, 0f, p0.Z);
        var p1 = At(xr, zr, thTo, s);
        float k = Smooth(u);
        return (p0.X + (p1.X - p0.X) * k, 0.10f * MathF.Sin(MathF.PI * u), p0.Z + (p1.Z - p0.Z) * k);
    }

    /// <summary>The torso's outline read off the SAME curve PaintBody draws: for each of 23 slices along the back, the top and the bottom.</summary>
    private sealed class Profile { public float[] X = null!, Top = null!, Bot = null!; }
    private static Profile? _profBody, _profCalf;
    private static Profile TorsoProfile(bool calf)
    {
        Profile? have = calf ? _profCalf : _profBody;
        if (have != null) return have;
        float[] v = [0.34f, 0.99f, 0.52f, 0.88f, 0.58f, 0.68f, 0.46f, 0.50f, 0.20f, 0.45f, -0.10f, 0.46f, -0.38f, 0.52f, -0.62f, 0.58f, -0.72f, 0.76f, -0.62f, 0.91f, -0.34f, 0.96f, 0.0f, 0.97f];
        var w = new Warp(new Pose(), calf);
        var pts = new PointF[v.Length / 2];
        for (int i = 0; i < pts.Length; i++) pts[i] = w.Q(v[2 * i], v[2 * i + 1]);
        using var path = new GraphicsPath();
        path.AddClosedCurve(pts, 0.5f);
        path.Flatten(null, 0.002f);
        PointF[] pp = path.PathPoints;
        float xmin = 9, xmax = -9;
        foreach (PointF q in pp) { xmin = MathF.Min(xmin, q.X); xmax = MathF.Max(xmax, q.X); }
        const int n = 23;
        var pr = new Profile { X = new float[n], Top = new float[n], Bot = new float[n] };
        for (int i = 0; i < n; i++)
        {
            float x = xmin + 0.012f + (xmax - xmin - 0.024f) * i / (n - 1), lo = 9, hi = -9;
            for (int j = 0; j < pp.Length; j++)
            {
                PointF a = pp[j], b = pp[(j + 1) % pp.Length];
                if ((a.X - x) * (b.X - x) <= 0 && a.X != b.X) { float y = a.Y + (b.Y - a.Y) * (x - a.X) / (b.X - a.X); lo = MathF.Min(lo, y); hi = MathF.Max(hi, y); }
            }
            if (hi < lo) { lo = 0.65f; hi = 0.75f; }
            pr.X[i] = x; pr.Top[i] = hi; pr.Bot[i] = lo;
        }
        if (calf) _profCalf = pr; else _profBody = pr;
        return pr;
    }

    /// <summary>Half the barrel's width at body-length position x: fullest at the ribs, slimmer toward the chest and the rump.</summary>
    private static float Wz(float x, bool calf)
    {
        float u = (x + 0.05f) / 0.80f;
        return 0.165f * MathF.Pow(MathF.Max(0.02f, 1f - u * u), 0.35f) * (calf ? 0.88f : 1f);
    }

    private static Color Mix(Color under, Color over, float a) =>
        Color.FromArgb((int)(under.R + (over.R - under.R) * a), (int)(under.G + (over.G - under.G) * a), (int)(under.B + (over.B - under.B) * a));

    private static float Cross(PointF o, PointF a, PointF b) => (a.X - o.X) * (b.Y - o.Y) - (a.Y - o.Y) * (b.X - o.X);

    [ThreadStatic] private static PointF[]? _hp, _hh;

    /// <summary>Fills the smallest convex shape that holds both rings (the rubber band round two coins). With b == null: the one ring.</summary>
    private static void Hull(Graphics g, Color col, PointF[] a, PointF[]? b = null)
    {
        int n = a.Length + (b?.Length ?? 0);
        var p = _hp ??= new PointF[64];
        var hl = _hh ??= new PointF[128];
        a.CopyTo(p, 0); b?.CopyTo(p, a.Length);
        Array.Sort(p, 0, n, HullOrder.Instance);
        int k = 0;
        for (int i = 0; i < n; i++) { while (k >= 2 && Cross(hl[k - 2], hl[k - 1], p[i]) <= 0) k--; hl[k++] = p[i]; }
        for (int i = n - 2, t = k + 1; i >= 0; i--) { while (k >= t && Cross(hl[k - 2], hl[k - 1], p[i]) <= 0) k--; hl[k++] = p[i]; }
        if (k - 1 < 3) return;
        var br = Br(col);
        g.FillPolygon(br, new ReadOnlySpan<PointF>(hl, 0, k - 1));
    }
    private sealed class HullOrder : IComparer<PointF>
    {
        public static readonly HullOrder Instance = new();
        public int Compare(PointF u, PointF v) => u.X != v.X ? u.X.CompareTo(v.X) : u.Y.CompareTo(v.Y);
    }

    private static PointF[] Ring(float cx, float cy, float ax, float ay, float bx, float by)
    {
        const int n = 12;
        var r = new PointF[n];
        for (int i = 0; i < n; i++) { float t = i * MathF.Tau / n, cs = MathF.Cos(t), sg = MathF.Sin(t); r[i] = new PointF(cx + cs * ax + sg * bx, cy + cs * ay + sg * by); }
        return r;
    }

    /// <summary>
    /// One whole animal turned to body yaw phiDeg (0 = the side view facing right, 90 = facing us or facing away, 180 = facing left),
    /// seen from the side s (+1 turns toward us, -1 turns away). f: the head pitch (0 nose up to 1 nose in the snow).
    /// The sheet's origin is the middle of the body on the ground; y is up, in body heights.
    /// </summary>
    private static void PaintFigure(Graphics g, ReindeerKind kind, int h, float phiDeg, int side, float f)
    {
        bool calf = kind == ReindeerKind.Calf, bull = kind == ReindeerKind.Bull;
        float pw = MathF.Max(0.012f, 1.0f / h);
        float phi = phiDeg * MathF.PI / 180f, c = MathF.Cos(phi), sn = MathF.Sin(phi), s = side;
        float psi = MathF.Min(MathF.PI, phi + 0.30f * MathF.Sin(phi));       // the head leads the body round
        float cp = MathF.Cos(psi), sp = MathF.Sin(psi);
        Color coat = calf ? CoatCalf : Coat, dark = calf ? CoatCalfDark : CoatDark, far = calf ? CoatCalfFar : CoatFar;
        var warp = new Warp(new Pose(), calf);
        var items = new List<(float D, Action Paint)>();

        void Bone(PointF a, PointF b, float wa, float wb, Color col)
        {
            wa = MathF.Max(wa, 1.6f * pw); wb = MathF.Max(wb, 1.3f * pw);
            float dx = b.X - a.X, dy = b.Y - a.Y, l = MathF.Sqrt(dx * dx + dy * dy) + 1e-5f, nx = -dy / l, ny = dx / l;
            var br = Br(col);
            g.FillPolygon(br, [P(a.X + nx * wa / 2, a.Y + ny * wa / 2), P(b.X + nx * wb / 2, b.Y + ny * wb / 2), P(b.X - nx * wb / 2, b.Y - ny * wb / 2), P(a.X - nx * wa / 2, a.Y - ny * wa / 2)]);
            g.FillEllipse(br, a.X - wa / 2, a.Y - wa / 2, wa, wa);
            g.FillEllipse(br, b.X - wb / 2, b.Y - wb / 2, wb, wb);
        }

        // ---------------------------------------------------------------- the torso: 23 slices, each with its coat, saddle, belly, rump patch and moon rim
        Profile pr = TorsoProfile(calf);
        int n = pr.X.Length;
        var rBase = new PointF[n][]; var rSad = new PointF[]?[n]; var rBel = new PointF[]?[n]; var rPat = new PointF[]?[n]; var rRim = new PointF[]?[n];
        Color patC = Mix(coat, Cream, 0.84f), rimC = Mix(coat, Color.FromArgb(196, 214, 246), 0.59f);
        for (int i = 0; i < n; i++)
        {
            float x = pr.X[i], yt = pr.Top[i], yb = pr.Bot[i], hh = MathF.Max(0.004f, (yt - yb) / 2), yc = (yt + yb) / 2, wz = Wz(x, calf);
            float cx = x * c, bx = -s * wz * sn;
            rBase[i] = Ring(cx, yc, 0, hh, bx, 0);
            float lo = MathF.Max(yb, yt - 0.14f), hi = yt - 0.012f;
            if (hi - lo > 0.02f) rSad[i] = Ring(cx, (lo + hi) / 2, 0, (hi - lo) / 2, bx * 0.85f, 0);
            lo = yb + 0.01f; hi = MathF.Min(yt, yb + 0.095f);
            if (hi - lo > 0.02f) rBel[i] = Ring(cx, (lo + hi) / 2, 0, (hi - lo) / 2, bx * 0.8f, 0);
            if (x < (calf ? -0.40f : -0.46f) && x > (calf ? -0.58f : -0.66f))
            {
                lo = MathF.Max(yb, 0.62f); hi = MathF.Min(yt, 0.86f);
                if (hi - lo > 0.02f) rPat[i] = Ring(cx, (lo + hi) / 2, 0, (hi - lo) / 2, bx, 0);
            }
            if (yt - yb > 0.05f && sn < 0.5f) rRim[i] = Ring(cx, yt - 0.012f, 0, 0.012f, bx * 0.55f, 0);
        }
        // Near the side view the slices all lie at about one depth, so the whole back is painted in two sweeps (every slice's coat, then
        // every slice's layers): painting slice by slice let each neighbour's coat nibble the last one's saddle into stripes. From about
        // 58 degrees on, the slices are real depths (the chest in front of the ribs) and go into the depth sort one by one.
        void Layers(int i0)
        {
            if (rSad[i0] != null && rSad[i0 + 1] != null) Hull(g, dark, rSad[i0]!, rSad[i0 + 1]!);
            if (rBel[i0] != null && rBel[i0 + 1] != null) Hull(g, Cream, rBel[i0]!, rBel[i0 + 1]!);
            if (rPat[i0] != null && rPat[i0 + 1] != null) Hull(g, patC, rPat[i0]!, rPat[i0 + 1]!);
            if (rRim[i0] != null && rRim[i0 + 1] != null) Hull(g, rimC, rRim[i0]!, rRim[i0 + 1]!);
        }
        if (sn < 0.85f)
            items.Add((0f, () =>
            {
                for (int i = 0; i < n - 1; i++) Hull(g, coat, rBase[i], rBase[i + 1]);
                for (int i = 0; i < n - 1; i++) Layers(i);
            }));
        else
        {
            // Facing us or away: each slice is a depth of its own, but each LAYER (saddle, belly, rump patch) is one piece laid at the depth
            // of where it lives, so the nearer slices cover it and it does not break into ribs.
            for (int i = 0; i < n - 1; i++)
            {
                int i0 = i;
                items.Add((s * 0.5f * (pr.X[i] + pr.X[i + 1]) * sn, () => Hull(g, coat, rBase[i0], rBase[i0 + 1])));
            }
            void Layer(float atX, PointF[]?[] ring, Color col) => items.Add((s * atX * sn + 0.001f, () =>
            {
                for (int i = 0; i < n - 1; i++) if (ring[i] != null && ring[i + 1] != null) Hull(g, col, ring[i]!, ring[i + 1]!);
            }));
            Layer(-0.56f, rPat, patC); Layer(0.0f, rSad, dark); Layer(0.45f, rBel, Cream);
        }

        // ---------------------------------------------------------------- the tail: a short white tuft, hanging down and back
        {
            PointF root = warp.Q(-0.68f, 0.83f);
            PointF a = P(root.X * c, root.Y), b = P((root.X - 0.07f) * c, root.Y - 0.075f);
            items.Add((s * root.X * sn + 0.02f, () => Bone(a, b, 0.085f, 0.06f, White)));
        }

        // ---------------------------------------------------------------- the legs: planted feet, real knees
        Vector3 fwd = new(c, 0, s * sn);
        float ls = calf ? 1.07f : 1f;
        Vector3 Joint(Vector3 a, Vector3 b, float bone, Vector3 bend)
        {
            Vector3 d = b - a; float dl = d.Length();
            if (dl < 1e-4f) return a;
            float len = MathF.Min(dl, 2 * bone - 0.002f);
            Vector3 u = d / dl, nn = bend - Vector3.Dot(bend, u) * u;
            nn = nn.LengthSquared() < 1e-6f ? new Vector3(0, 0, 1) : Vector3.Normalize(nn);
            return a + u * (len / 2) + nn * MathF.Sqrt(MathF.Max(0f, bone * bone - len * len / 4));
        }
        static PointF V(Vector3 v) => new(v.X, v.Y);
        for (int j = 0; j < 4; j++)
        {
            bool hind = j == 0 || j == 2;
            PointF hb = warp.Q(HipRest[j], hind ? 0.64f : 0.58f);
            var hip = new Vector3(hb.X * c - s * LegZ[j] * sn, hb.Y, s * hb.X * sn + LegZ[j] * c);
            var (fx, lift, fz) = FootWorld(j, phiDeg, side, calf);
            var foot = new Vector3(fx, 0.02f + lift, fz);
            float tt = Math.Clamp((hip.Z + 0.06f) / 0.12f, 0f, 1f);          // 1 = on the lit side, 0 = on the far side
            Color upper = Mix(far, coat, tt), lower = Mix(LegFar, LegPale, tt);
            items.Add((hip.Z, () =>
            {
                if (!hind)
                {
                    Vector3 knee = Joint(hip, foot, 0.29f * ls, fwd);
                    Bone(V(hip), V(knee), 0.125f, 0.082f, upper);
                    Bone(V(knee), V(foot), 0.072f, 0.056f, lower);
                }
                else
                {
                    float pl = Math.Clamp(0.035f - 2.0f * lift, -0.5f, 0.7f);
                    Vector3 hock = foot + 0.25f * ls * (MathF.Cos(pl) * Vector3.UnitY - MathF.Sin(pl) * fwd);
                    Vector3 stifle = Joint(hip, hock, 0.27f * ls, fwd);
                    Bone(V(hip), V(stifle), 0.17f, 0.105f, upper);
                    Bone(V(stifle), V(hock), 0.105f, 0.075f, upper);
                    Bone(V(hock), V(foot), 0.072f, 0.056f, lower);
                }
                float k = MathF.Abs(c) + 0.9f * MathF.Abs(sn), cx = foot.X + 0.01f * c, y = foot.Y;
                var hb2 = Br(Hoof);
                g.FillPolygon(hb2, [P(cx - 0.040f * k, y + 0.032f), P(cx + 0.050f * k, y + 0.028f), P(cx + 0.058f * k, y - 0.022f), P(cx - 0.040f * k, y - 0.022f)]);
            }));
        }

        // ---------------------------------------------------------------- the neck, the mane and the head
        {
            float na = NeckAngle(f), ha = HeadAxis(f), nlen = NeckLen(f, kind);
            PointF rootB = warp.Q(NeckX, NeckY);
            float w0 = bull ? 0.46f : calf ? 0.32f : 0.38f, w1 = bull ? 0.31f : calf ? 0.22f : 0.27f;
            float zb0 = bull ? 0.19f : calf ? 0.13f : 0.16f;
            const int N = 6;
            var nc = new PointF[N + 1];
            var yw = new float[N + 1];
            nc[0] = P(rootB.X * c, rootB.Y);
            yw[0] = phi;
            for (int i = 1; i <= N; i++)
            {
                float tm = (i - 0.5f) / N, ym = phi + (psi - phi) * Smooth(tm);
                yw[i] = phi + (psi - phi) * Smooth(i / (float)N);
                nc[i] = P(nc[i - 1].X + nlen / N * MathF.Sin(na) * MathF.Cos(ym), nc[i - 1].Y + nlen / N * MathF.Cos(na));
            }
            var rNeck = new PointF[N + 1][]; var rMane = new PointF[N + 1][];
            float nx = MathF.Cos(na), ny = -MathF.Sin(na);
            for (int i = 0; i <= N; i++)
            {
                float t = i / (float)N, ww = w0 + (w1 - w0) * t, zb = zb0 * (1f - 0.28f * t), cy = MathF.Cos(yw[i]), sy = MathF.Sin(yw[i]);
                rNeck[i] = Ring(nc[i].X, nc[i].Y, nx * cy * ww / 2, ny * ww / 2, -s * zb * sy, 0);
                float hm = (0.13f * (1 - t) + 0.06f) * (bull ? 1.2f : 1f);
                rMane[i] = Ring(nc[i].X + nx * cy * 0.36f * ww, nc[i].Y + ny * 0.36f * ww - 0.045f, 0, hm, -s * zb * 0.85f * sy, 0);
            }
            PointF top = nc[N];
            float hmx = 0.10f * MathF.Sin(ha), hmy = 0.10f * MathF.Cos(ha);
            (float X, float Y, float D) HP(float u, float v, float z)
            {
                float lx = hmx + u * MathF.Sin(ha) - v * MathF.Cos(ha), ly = hmy + u * MathF.Cos(ha) + v * MathF.Sin(ha);
                return (top.X + lx * cp - s * z * sp, top.Y + ly, s * lx * sp + z * cp);
            }
            PointF[] HRing(float u, float v, float z, float au, float av, float bz)
            {
                const int m = 12;
                var r = new PointF[m];
                for (int i = 0; i < m; i++) { float t = i * MathF.Tau / m, cs = MathF.Cos(t), sg = MathF.Sin(t); var q = HP(u + cs * au, v + cs * av, z + sg * bz); r[i] = new PointF(q.X, q.Y); }
                return r;
            }
            float ml = MuzzleLen(kind);
            float sk = calf ? 0.20f : 0.17f, sh = calf ? 0.135f : 0.115f, sz = calf ? 0.075f : 0.095f;
            var hd = new List<(float D, Action Paint)>();

            // the skull
            {
                const int m = 7;
                var rs = new PointF[m][];
                for (int i = 0; i < m; i++) { float u = -sk + 2 * sk * i / (m - 1), r = MathF.Sqrt(MathF.Max(0.02f, 1 - (u / sk) * (u / sk))); rs[i] = HRing(u, 0, 0, 0, sh * r, sz * r); }
                hd.Add((0f, () => { for (int i = 0; i < m - 1; i++) Hull(g, coat, rs[i], rs[i + 1]); }));
            }
            // the muzzle: broad, squared off at the nose, with the pale band and the dark nose pad
            {
                (float u, float top, float bot, float bz)[] ms = [(0f, 0.085f, -0.10f, 0.075f), (ml * 0.55f, 0.072f, -0.088f, 0.072f), (ml, 0.058f, -0.062f, 0.062f), (ml + 0.022f, 0.035f, -0.038f, 0.05f), (ml + 0.035f, 0.010f, -0.012f, 0.02f)];
                var rm = new PointF[ms.Length][];
                for (int i = 0; i < ms.Length; i++) rm[i] = HRing(ms[i].u, (ms[i].top + ms[i].bot) / 2, 0, 0, (ms[i].top - ms[i].bot) / 2, ms[i].bz);
                (float u, float vc, float av, float bz)[] bs = [(ml - 0.09f, -0.015f, 0.055f, 0.067f), (ml - 0.045f, -0.011f, 0.054f, 0.068f), (ml, -0.0075f, 0.0525f, 0.066f)];
                var rb = new PointF[bs.Length][];
                for (int i = 0; i < bs.Length; i++) rb[i] = HRing(bs[i].u, bs[i].vc, 0, 0, bs[i].av, bs[i].bz);
                (float u, float av, float bz)[] ns = [(ml - 0.03f, 0.030f, 0.040f), (ml + 0.01f, 0.0375f, 0.050f), (ml + 0.05f, 0.020f, 0.030f)];
                var rn = new PointF[ns.Length][];
                for (int i = 0; i < ns.Length; i++) rn[i] = HRing(ns[i].u, -0.0075f, 0, 0, ns[i].av, ns[i].bz);
                Color band = Mix(coat, Color.FromArgb(190, 172, 156), 0.80f);
                hd.Add((HP(ml * 0.5f, 0, 0).D, () =>
                {
                    for (int i = 0; i < ms.Length - 1; i++) Hull(g, coat, rm[i], rm[i + 1]);
                    for (int i = 0; i < bs.Length - 1; i++) Hull(g, band, rb[i], rb[i + 1]);
                    for (int i = 0; i < ns.Length - 1; i++) Hull(g, Nose, rn[i], rn[i + 1]);
                }));
            }
            // the eyes: on the forward sides of the skull, drawn only where that side faces us
            hd.Add((9f, () =>
            {
                for (int sd = -1; sd <= 1; sd += 2)
                {
                    float nlx = 0.5f * MathF.Sin(ha) - 0.3f * MathF.Cos(ha), zn = s * nlx * sp + sd * 0.85f * cp;
                    if (zn < -0.25f) continue;
                    var e = HP(0.075f, 0.05f, sd * 0.075f);
                    float r = MathF.Max(0.029f, 1.5f * pw), wf = 0.4f + 0.6f * Math.Clamp((zn + 0.25f) / 1.0f, 0f, 1f);
                    using var eb = new SolidBrush(Nose);
                    g.FillEllipse(eb, e.X - r * wf, e.Y - r, 2 * r * wf, 2 * r);
                    using var gl = new SolidBrush(Color.White);
                    float gr = MathF.Max(0.009f, 0.65f * pw);
                    g.FillEllipse(gl, e.X + r * wf * 0.15f, e.Y + r * 0.35f, 2 * gr, 2 * gr);
                }
            }));
            // the ears: a round leaf each, the near one pink inside
            float earAng = (f < 0.18f ? 95f : 118f) * MathF.PI / 180f;
            for (int sd = -1; sd <= 1; sd += 2)
            {
                float ang = earAng + (sd < 0 ? 28f * MathF.PI / 180f : 0f), el = 0.15f;
                float bu = -0.095f, bv = 0.08f, bzz = sd * 0.075f;
                float tu = bu + el * MathF.Cos(ang), tv = bv + el * MathF.Sin(ang), tz = sd * (0.075f + 0.09f);
                var re = new PointF[3][]; var ri = new PointF[3][];
                for (int i = 0; i < 3; i++)
                {
                    float t = i / 2f, u = bu + (tu - bu) * t, v = bv + (tv - bv) * t, z = bzz + (tz - bzz) * t, rad = 0.040f * (1f - 0.55f * t * t);
                    re[i] = HRing(u, v, z, 0, rad, rad * 0.85f);
                    ri[i] = HRing(u, v, z + sd * 0.012f, 0, rad * 0.55f, rad * 0.5f);
                }
                Color ec = sd < 0 ? EarOut : coat, ic = sd < 0 ? Color.FromArgb(150, 110, 100) : EarIn;
                float zn = sd * cp, vis = s * sp;
                bool inner = zn > 0.15f || vis > 0.35f;
                hd.Add((HP((bu + tu) / 2, (bv + tv) / 2, (bzz + tz) / 2).D, () =>
                {
                    Hull(g, ec, re[0], re[1]); Hull(g, ec, re[1], re[2]);
                    if (inner) { Hull(g, ic, ri[0], ri[1]); Hull(g, ic, ri[1], ri[2]); }
                }));
            }
            // the antlers
            if (!calf)
            {
                bool away = s * sp < -0.4f;
                for (int sd = -1; sd <= 1; sd += 2)
                {
                    int sdd = sd;
                    float sc = bull ? 1.0f : 0.58f, asz = sdd < 0 ? sc * 0.90f : sc;
                    PointF at = sdd < 0 ? P(-0.14f, 0.11f) : P(-0.02f, 0.09f);
                    Color col = sdd < 0 ? AntlerFar : Antler;
                    hd.Add((away ? 5f : -5f + sdd * 0.01f, () => PaintAntler3(g, HP, asz, col, at, bull, pw, sdd)));
                }
            }
            hd.Sort((p, q) => p.D.CompareTo(q.D));

            bool maneBehind = s * sp < -0.35f;
            items.Add(((s > 0 || phiDeg < 40f) ? 1000f : -1000f, () =>
            {
                if (maneBehind) for (int i = 0; i < N; i++) Hull(g, Mane, rMane[i], rMane[i + 1]);
                for (int i = 0; i < N; i++) Hull(g, coat, rNeck[i], rNeck[i + 1]);
                if (!maneBehind) for (int i = 0; i < N; i++) Hull(g, Mane, rMane[i], rMane[i + 1]);
                foreach (var it in hd) it.Paint();
            }));
        }

        items.Sort((p, q) => p.D.CompareTo(q.D));
        foreach (var it in items) it.Paint();
    }

    /// <summary>One antler in 3D: PaintAntler's curves, each point given a sideways place (the beam climbs up AND out, the brow tine stays near the middle of the face).</summary>
    private static void PaintAntler3(Graphics g, Func<float, float, float, (float X, float Y, float D)> hp, float s, Color c, PointF at, bool bull, float pw, int zs)
    {
        float wBeam = MathF.Max(bull ? 0.050f : 0.034f, 2.3f * pw);
        using var penB = new Pen(c, wBeam) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        using var penT = new Pen(c, MathF.Max(wBeam * 0.72f, 1.8f * pw)) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        using var penTip = new Pen(c, MathF.Max(wBeam * 0.5f, 1.5f * pw)) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        // sideways place from the height above the root: out and up to 0.6 of a head-length, then curving back in
        float Zup(float y) { float yy = MathF.Max(0f, y * s); return zs * (0.06f + 0.60f * MathF.Min(yy, 0.6f) - 0.30f * MathF.Max(0f, yy - 0.6f)); }
        PointF Pt(float x, float y, bool mid)
        {
            var q = hp(at.X + x * s, at.Y + y * s, mid ? zs * 0.05f : Zup(y));
            return new PointF(q.X, q.Y);
        }
        PointF[] Cv(bool mid, params float[] v)
        {
            var r = new PointF[v.Length / 2];
            for (int i = 0; i < r.Length; i++) r[i] = Pt(v[2 * i], v[2 * i + 1], mid);
            return r;
        }
        g.DrawCurve(penB, Cv(false, 0, 0, -0.10f, 0.20f, -0.20f, 0.42f, -0.19f, 0.62f), 0.5f);
        g.DrawCurve(penT, Cv(false, -0.19f, 0.62f, -0.06f, 0.80f, 0.14f, 0.90f), 0.5f);
        g.DrawCurve(penT, Cv(true, 0.02f, 0.07f, 0.13f, 0.15f, 0.24f, 0.14f), 0.5f);                // brow tine over the face
        using (var shovel = new SolidBrush(c))
        using (var sp = new GraphicsPath())
        {
            sp.AddClosedCurve(Cv(true, 0.18f, 0.17f, 0.27f, 0.20f, 0.33f, 0.13f, 0.26f, 0.08f), 0.4f);
            g.FillPath(shovel, sp);
        }
        g.DrawCurve(penT, Cv(false, -0.17f, 0.36f, -0.04f, 0.47f, 0.09f, 0.49f), 0.5f);
        if (bull)
        {
            g.DrawCurve(penT, Cv(false, -0.20f, 0.52f, -0.33f, 0.65f, -0.36f, 0.78f), 0.5f);
            g.DrawCurve(penTip, Cv(false, -0.10f, 0.76f, -0.13f, 0.92f, -0.09f, 1.04f), 0.5f);
            g.DrawCurve(penTip, Cv(false, -0.24f, 0.30f, -0.36f, 0.40f, -0.40f, 0.52f), 0.5f);
            using var palm = new SolidBrush(c);
            using var pp = new GraphicsPath();
            pp.AddClosedCurve(Cv(false, 0.08f, 0.84f, 0.20f, 0.97f, 0.32f, 0.92f, 0.28f, 0.80f, 0.16f, 0.78f), 0.4f);
            g.FillPath(palm, pp);
        }
    }

    // ================================================================ the pose sheet (PECKWORKS_SHEET=reindeer;file.png)

    /// <summary>
    /// Writes every page of every flip-book to one PNG: each body page with a head on its neck, on the snow colour, in labelled
    /// rows; then a strip of the walk and the trot on the night blue. Judge the animal on this at 4x before judging it in the scene.
    /// </summary>
    public static void WriteSheet(string path, ReindeerSet[] sets)
    {
        const int Snow = unchecked((int)0xFFDEE9FC), Night = unchecked((int)0xFF14245C);
        var rows = new List<(string Label, List<(ReindeerSet S, RPage B, RPage? Hd, int HeadPitch)> Cells, int Bg)>();
        foreach (ReindeerSet s in sets)
        {
            string nm = s.Kind.ToString();
            int hp25 = (int)MathF.Round(0.25f * (HeadPages - 1)), hp60 = (int)MathF.Round(0.6f * (HeadPages - 1)), hp40 = (int)MathF.Round(0.4f * (HeadPages - 1));
            void Row(string label, RPage[] pages, int hp, int bg = Snow, int from = 0, int count = -1)
            {
                var cells = new List<(ReindeerSet, RPage, RPage?, int)>();
                int n = count < 0 ? pages.Length : count;
                for (int i = from; i < from + n && i < pages.Length; i++) if (pages[i] != null) cells.Add((s, pages[i], s.Head[hp, 0], hp));
                if (cells.Count > 0) rows.Add(($"{nm} {label}", cells, bg));
            }
            Row("walk 0-11", s.Walk, hp25, Snow, 0, 12);
            Row("walk 12-23", s.Walk, hp25, Snow, 12, 12);
            Row("trot", s.Trot, hp25);
            Row("trot in", s.TrotIn, hp25);
            Row("settle", s.Settle, hp25);
            Row("paw", s.Paw, hp60);
            Row("down", s.Down, hp25);
            Row("up", s.Up, hp25);
            Row("shake", s.Shake, hp40);
            Row("play (calf)", s.Gam, hp25);
            var heads = new List<(ReindeerSet, RPage, RPage?, int)>();
            var blank = new RPage { S = Sprite.FromPixels(1, 1, [0u]), NX = 0, NY = 0 };
            for (int j = 0; j < HeadPages; j += 1) heads.Add((s, blank, s.Head[j, 0], j));
            heads.Add((s, blank, s.Head[hp25, 1], hp25)); heads.Add((s, blank, s.Head[hp25, 2], hp25));
            rows.Add(($"{nm} heads (pitch 0 to 1, then chew, then ear flick)", heads, Snow));
            Row("walk strip on night", s.Walk, hp25, Night, 0, 12);
            Row("trot strip on night", s.Trot, hp25, Night);
        }
        // layout: each cell is as wide as the biggest body sheet plus the head's reach, each row as tall as its tallest head above a body
        int cw = 0, ch = 0, labelW = 150;
        foreach (var s in sets) { cw = Math.Max(cw, s.BodyW + (int)(0.9f * s.H) + 4); ch = Math.Max(ch, s.HeadOy + s.BodyH + 6); }
        int width = labelW + 12 * cw + 8, height = rows.Count * ch + 8;
        var pix = new uint[width * height];
        using var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        int y0 = 4;
        foreach (var row in rows)
        {
            for (int y = y0; y < y0 + ch && y < height; y++) Array.Fill(pix, (uint)row.Bg, y * width, width);
            for (int i = 0; i < row.Cells.Count; i++)
            {
                var (s, b, hd, _) = row.Cells[i];
                int cx = labelW + (i % 12) * cw + (int)(0.1f * s.H);
                int bx = cx + 2, by = y0 + (ch - s.BodyH - 4);
                Blit(pix, width, height, s.Shadow, bx + s.BodyOx - s.Shadow.Width / 2, by + s.BodyOy - s.Shadow.Height / 2);
                bool blankBody = b.S.Width == 1;
                if (!blankBody) Blit(pix, width, height, b.S, bx + s.BodyOx - b.OX, by + s.BodyOy - b.OY);
                if (hd != null)
                {
                    int nxp = bx + s.BodyOx + (int)MathF.Round(b.NX * s.H), nyp = by + s.BodyOy - (int)MathF.Round(b.NY * s.H);
                    if (blankBody) { nxp = bx + s.BodyOx; nyp = by + s.BodyOy - (int)(0.6f * s.H); }
                    Blit(pix, width, height, hd.S, nxp - hd.OX, nyp - hd.OY);
                }
            }
            y0 += ch;
        }
        var data = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        var raw = new int[pix.Length];
        Buffer.BlockCopy(pix, 0, raw, 0, pix.Length * 4);
        System.Runtime.InteropServices.Marshal.Copy(raw, 0, data.Scan0, raw.Length);
        bmp.UnlockBits(data);
        using (var g = Graphics.FromImage(bmp))
        {
            using var f = new Font("Segoe UI", 8f);
            y0 = 4;
            foreach (var row in rows)
            {
                using var br = new SolidBrush(row.Bg == Night ? Color.White : Color.FromArgb(40, 40, 60));
                g.DrawString(row.Label, f, br, new RectangleF(2, y0 + 2, labelW - 4, 60));
                y0 += ch;
            }
        }
        bmp.Save(path, ImageFormat.Png);
    }

    private static RPage FlipPage(RPage p)
    {
        Sprite sp = p.S; int w = sp.Width, hh = sp.Height; ReadOnlySpan<uint> src = sp.Argb;
        var px = new uint[w * hh];
        for (int y = 0; y < hh; y++) for (int x = 0; x < w; x++) px[y * w + (w - 1 - x)] = src[y * w + x];
        return new RPage { S = Sprite.FromPixels(w, hh, px), NX = -p.NX, NY = p.NY, OX = w - 1 - p.OX, OY = p.OY };
    }

    /// <summary>
    /// The turn's pose sheet: for each animal, the half turn toward us (pages 0 to TurnSteps in two rows, the ends being the ordinary
    /// standing pages, the last one mirrored), the half turn away from us, the head levels at the head-on page, and a night row.
    /// </summary>
    public static void WriteTurnSheet(string path, ReindeerSet[] sets)
    {
        const int Snow = unchecked((int)0xFFDEE9FC), Night = unchecked((int)0xFF14245C);
        const int Cols = 11;
        var rows = new List<(string Label, List<(ReindeerSet S, RPage B, RPage? Hd)> Cells, int Bg)>();
        foreach (ReindeerSet s in sets)
        {
            string nm = s.Kind.ToString();
            RPage Body(int side, int lv, int k)
            {
                if (k == 0) return s.Stand;
                if (k == TurnSteps) return FlipPage(s.Stand);
                return s.Fig[side, lv, k]!;
            }
            RPage? Head(int lv, int k) => k == 0 ? s.Head[TurnHp[lv], 0] : k == TurnSteps ? FlipPage(s.Head[TurnHp[lv], 0]) : null;
            void Half(string label, int side, int lv, int bg)
            {
                for (int part = 0; part < 2; part++)
                {
                    var cells = new List<(ReindeerSet, RPage, RPage?)>();
                    for (int k = part * 11; k < Math.Min(TurnSteps + 1, part * 11 + 11); k++) cells.Add((s, Body(side, lv, k), Head(lv, k)));
                    rows.Add(($"{nm} {label} {(part == 0 ? "0-10" : "11-20")} (head level {lv})", cells, bg));
                }
            }
            Half("turn toward us", 0, 2, Snow);
            Half("turn away from us", 1, 2, Snow);
            var lvls = new List<(ReindeerSet, RPage, RPage?)>();
            for (int lv = 0; lv < TurnLevels; lv++) lvls.Add((s, s.Fig[0, lv, TurnSteps / 2]!, null));
            for (int lv = 0; lv < TurnLevels; lv++) lvls.Add((s, s.Fig[1, lv, TurnSteps / 2]!, null));
            rows.Add(($"{nm} head-on (toward us), then tail-on, head levels 0 to 6", lvls, Snow));
            Half("turn toward us, night", 0, 1, Night);
        }
        int cw = 0, ch = 0, labelW = 150;
        foreach (var s in sets) { cw = Math.Max(cw, s.FigW); ch = Math.Max(ch, s.FigH); }
        int width = labelW + Cols * cw + 8, height = rows.Count * ch + 8;
        var pix = new uint[width * height];
        using var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        int y0 = 4;
        foreach (var row in rows)
        {
            for (int y = y0; y < y0 + ch && y < height; y++) Array.Fill(pix, (uint)row.Bg, y * width, width);
            for (int i = 0; i < row.Cells.Count; i++)
            {
                var (s, b, hd) = row.Cells[i];
                int cx = labelW + (i % Cols) * cw + cw / 2, gy = y0 + ch - (s.FigH - s.FigOy) + 0;
                Blit(pix, width, height, s.Shadow, cx - s.Shadow.Width / 2, gy - s.Shadow.Height / 2);
                Blit(pix, width, height, b.S, cx - b.OX, gy - b.OY);
                if (hd != null)
                {
                    RPage bodyRef = b;
                    int nxp = cx + (int)MathF.Round(bodyRef.NX * s.H), nyp = gy - (int)MathF.Round(bodyRef.NY * s.H);
                    Blit(pix, width, height, hd.S, nxp - hd.OX, nyp - hd.OY);
                }
            }
            y0 += ch;
        }
        var data = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        var raw = new int[pix.Length];
        Buffer.BlockCopy(pix, 0, raw, 0, pix.Length * 4);
        System.Runtime.InteropServices.Marshal.Copy(raw, 0, data.Scan0, raw.Length);
        bmp.UnlockBits(data);
        using (var g = Graphics.FromImage(bmp))
        {
            using var f = new Font("Segoe UI", 8f);
            y0 = 4;
            foreach (var row in rows)
            {
                using var br = new SolidBrush(row.Bg == Night ? Color.White : Color.FromArgb(40, 40, 60));
                g.DrawString(row.Label, f, br, new RectangleF(2, y0 + 2, labelW - 4, 60));
                y0 += ch;
            }
        }
        bmp.Save(path, ImageFormat.Png);
    }

    private static void Blit(uint[] dst, int dw, int dh, Sprite s, int x0, int y0)
    {
        ReadOnlySpan<uint> src = s.Argb;
        for (int y = 0; y < s.Height; y++)
        {
            int dy = y0 + y;
            if (dy < 0 || dy >= dh) continue;
            for (int x = 0; x < s.Width; x++)
            {
                int dx = x0 + x;
                if (dx < 0 || dx >= dw) continue;
                uint c = src[y * s.Width + x];
                int a = (int)(c >> 24);
                if (a == 0) continue;
                uint bg = dst[dy * dw + dx];
                int r = (int)((bg >> 16) & 255) + ((((int)(c >> 16) & 255) - (int)((bg >> 16) & 255)) * a >> 8);
                int gg = (int)((bg >> 8) & 255) + ((((int)(c >> 8) & 255) - (int)((bg >> 8) & 255)) * a >> 8);
                int b = (int)(bg & 255) + ((((int)c & 255) - (int)(bg & 255)) * a >> 8);
                dst[dy * dw + dx] = 0xFF000000u | (uint)(r << 16) | (uint)(gg << 8) | (uint)b;
            }
        }
    }
}
