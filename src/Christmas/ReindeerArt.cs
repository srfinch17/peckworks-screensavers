using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
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
    public RPage[] Turn = new RPage[ReindeerArt.TurnPages];
    public RPage[] TrotIn = new RPage[ReindeerArt.TrotInPages];     // the square stance to the trot's first page (and back): a start and a stop that share pages with the trot       // the square stance narrowed about the feet
    public RPage[,] Head = new RPage[ReindeerArt.HeadPages, ReindeerArt.HeadKinds];
    public PointF[] Nose = new PointF[ReindeerArt.HeadPages];     // the nostril on each head page (body heights from the neck's root, y up)
    public RPage[,] TurnHead = new RPage[ReindeerArt.TurnPitches, ReindeerArt.TurnPages];
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
    public const int WalkPages = 24, TrotPages = 16, SettlePages = 8, PawPages = 16, LiePages = 12, ShakePages = 9, GamPages = 6, TurnPages = 4, TrotInPages = 6;
    public const int HeadPages = 16, HeadKinds = 3, TurnPitches = 4;   // head kinds: 0 jaw closed, 1 jaw open (chewing), 2 near ear flicked back
    public static readonly float[] TurnSquash = [0.78f, 0.50f, 0.28f, 0.12f];
    public static readonly float[] TurnPitch = [0f, 0.25f, 0.60f, 1f];   // the head's tilt on the turn pages

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

    public static ReindeerSet Build(int h, ReindeerKind kind)
    {
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
            var full = Sprite.Paint(set.BodyW, set.BodyH, g =>
            {
                g.TranslateTransform(set.BodyOx, set.BodyOy);
                g.ScaleTransform(h, -h);                       // body heights, y UP
                neck = PaintBody(g, pose, kind, h);
            });
            page.S = Crop(full, set.BodyOx, set.BodyOy, out page.OX, out page.OY);   // a stamp costs its painted rows, but a smaller sheet is also less to hold and to copy
            page.NX = neck.X; page.NY = neck.Y;
            return page;
        }
        RPage HeadPage(float sx, Action<Graphics> paint)
        {
            Nap();
            var page = new RPage();
            var full = Sprite.Paint(set.HeadW, set.HeadH, g =>
            {
                g.TranslateTransform(set.HeadOx, set.HeadOy);
                g.ScaleTransform(h * sx, -h);
                paint(g);
            });
            page.S = Crop(full, set.HeadOx, set.HeadOy, out page.OX, out page.OY);
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
        // The turn pages: the square stance narrowed about the feet (a body coming round through the front view is a sliver).
        for (int t = 0; t < TurnPages; t++)
        {
            float sq = TurnSquash[t];
            var page = new RPage();
            var full = Sprite.Paint(set.BodyW, set.BodyH, g =>
            {
                g.TranslateTransform(set.BodyOx, set.BodyOy);
                g.ScaleTransform(h * sq, -h);
                PaintBody(g, new Pose(), kind, h);
            });
            page.S = Crop(full, set.BodyOx, set.BodyOy, out page.OX, out page.OY);
            page.NX = NeckX * sq; page.NY = NeckY;
            set.Turn[t] = page;
        }
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
        for (int pi = 0; pi < TurnPitches; pi++)
            for (int t = 0; t < TurnPages; t++)
            {
                float sq = TurnSquash[t], f = TurnPitch[pi];
                set.TurnHead[pi, t] = HeadPage(sq, g => PaintHead(g, f, 0, kind, h));
            }
        // The shadows: a flat soft ellipse on the snow, blue because it is night and the snow is lit by the moon and the sky.
        set.Shadow = ShadowSprite(h, 1.30f);
        set.ShadowLie = ShadowSprite(h, 1.55f);
        // The snow that settles along a lying back (drawn over the lying page with a rising opacity).
        set.CapLie = new RPage();
        set.CapLie.S = Crop(Sprite.Paint(set.BodyW, set.BodyH, g =>
        {
            g.TranslateTransform(set.BodyOx, set.BodyOy);
            g.ScaleTransform(h, -h);
            PaintCap(g, new Pose { WD = 0.40f, CD = 0.40f, FoldF = 1, FoldH = 1 }, kind);
        }), set.BodyOx, set.BodyOy, out set.CapLie.OX, out set.CapLie.OY);
        return set;
    }

    private static int _naps;
    /// <summary>Every fourth page the painter sleeps a moment so the build thread leaves a core to the frames being drawn (a test run of the whole herd's build took frames from 6 ms to 60 ms in the first seconds).</summary>
    private static void Nap() { if ((Interlocked.Increment(ref _naps) & 3) == 0) Thread.Sleep(1); }

    /// <summary>Cuts a painted sheet down to the rows and columns that hold paint; returns the new sprite and where the old origin is in it.</summary>
    private static Sprite Crop(Sprite s, int ox, int oy, out int nox, out int noy)
    {
        ReadOnlySpan<uint> a = s.Argb;
        int x0 = s.Width, y0 = s.Height, x1 = -1, y1 = -1;
        for (int y = 0; y < s.Height; y++)
            for (int x = 0; x < s.Width; x++)
                if ((a[y * s.Width + x] >> 24) != 0) { if (x < x0) x0 = x; if (x > x1) x1 = x; if (y < y0) y0 = y; if (y > y1) y1 = y; }
        if (x1 < 0) { nox = 0; noy = 0; return Sprite.FromPixels(1, 1, [0u]); }
        int w = x1 - x0 + 1, h = y1 - y0 + 1;
        var px = new uint[w * h];
        for (int y = 0; y < h; y++) a.Slice((y0 + y) * s.Width + x0, w).CopyTo(px.AsSpan(y * w, w));
        nox = ox - x0; noy = oy - y0;
        return Sprite.FromPixels(w, h, px);
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
            using var b = new SolidBrush(c);
            g.FillPath(b, path);
        }
        void Bone(PointF a, PointF b, float wa, float wb, Color c)
        {
            wa = MathF.Max(wa, 1.6f * pw); wb = MathF.Max(wb, 1.3f * pw);
            float dx = b.X - a.X, dy = b.Y - a.Y, l = MathF.Sqrt(dx * dx + dy * dy) + 1e-5f, nx = -dy / l, ny = dx / l;
            using var br = new SolidBrush(c);
            g.FillPolygon(br, [P(a.X + nx * wa / 2, a.Y + ny * wa / 2), P(b.X + nx * wb / 2, b.Y + ny * wb / 2), P(b.X - nx * wb / 2, b.Y - ny * wb / 2), P(a.X - nx * wa / 2, a.Y - ny * wa / 2)]);
            g.FillEllipse(br, a.X - wa / 2, a.Y - wa / 2, wa, wa);
            g.FillEllipse(br, b.X - wb / 2, b.Y - wb / 2, wb, wb);
        }
        void HoofAt(PointF f)
        {
            using var hb = new SolidBrush(Hoof);
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
        using var penT = new Pen(c, wBeam * 0.72f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        using var penTip = new Pen(c, wBeam * 0.5f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
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
            int hp25 = (int)MathF.Round(0.25f * (HeadPages - 1)), hp60 = (int)MathF.Round(0.6f * (HeadPages - 1)), hp100 = HeadPages - 1, hp40 = (int)MathF.Round(0.4f * (HeadPages - 1));
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
            var turn = new List<(ReindeerSet, RPage, RPage?, int)>();
            for (int t = 0; t < TurnPages; t++) turn.Add((s, s.Turn[t], s.TurnHead[1, t], hp25));
            rows.Add(($"{nm} turn", turn, Snow));
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
