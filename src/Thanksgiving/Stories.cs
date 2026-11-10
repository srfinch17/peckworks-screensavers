using System.Drawing.Drawing2D;
using Peckworks.Screensavers.Core;
using Peckworks.Screensavers.Core.Minds;
using Thanksgiving.Happenings;

namespace Thanksgiving;

/// <summary>
/// The machinery the little scenes are built on (his line: "i love the
/// little scenes that play out like the squirrels and the nuts. give me a
/// few of those"). Three parts:
///
///   - The CAST: every actor's flip-books, painted once and shared by all the
///     stories (the squirrel's bound, hop, turn, climb, sit, dig, chatter;
///     the mouse; the small birds; the dust, the nuts, an apple), and one
///     drawing routine that turns a segment of a script into a picture from
///     the clock alone.
///   - A SCRIPT: a list of tracks (one per actor), each a list of timed
///     segments (hop from here to there, run the rail, sit and look), with
///     builders that know how long each takes. A story's Begin rolls its
///     dice and writes its script; drawing a frame is "which segment is it,
///     how far through".
///   - The DIRECTOR: holds the stories, deals one every minute or two (never
///     while a happening holds a prop the story needs; it holds the props
///     itself while it plays through HappeningDirector.SceneClaims), and
///     draws the one that is on.
///
/// Everything on the ground is stamped against the near ground's depth
/// picture with the nearness of its own feet, so the pumpkins nearer than an
/// actor hide it and the rest stand behind it.
/// </summary>
internal enum Kind { Peek, Hop, Turn, Run, Rise, Climb, Mount, Sit, Chatter, Dazed, Dig, Mouse, MousePeek, Fracas, Bird, BirdFly, Still }

/// <summary>
/// The residents' shared test log (PECKWORKS_MINDLOG names a file): the flock, the herd and the patch write their
/// frames, rolls and reactions to one handle (two writers on one file refused the second open and the saver died).
/// </summary>
internal static class MindLog
{
    private static StreamWriter? _writer;
    private static bool _tried;

    public static StreamWriter? Open()
    {
        if (_tried) return _writer;
        _tried = true;
        string? path = Environment.GetEnvironmentVariable("PECKWORKS_MINDLOG");
        if (string.IsNullOrEmpty(path)) return null;
        _writer = new StreamWriter(new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite)) { AutoFlush = true };
        return _writer;
    }
}

/// <summary>One piece of one actor's part: what it does between T0 and T1, and where.</summary>
internal sealed class Seg
{
    public Kind Kind;
    public float T0, T1;
    public PointF From, To;             // the feet, in pixels (for Fracas: the cloud's centre on the ground)
    public int Dir = 1;                 // +1 faces right, -1 left (for Turn: the direction it ends facing)
    public bool Acorn, Look, Rail;      // carrying the nut; head up; on the rail or a trunk (the props stencil applies)
    public bool Up;                     // a climb with the head up (false: head first down); a mount that goes up onto the bark (false: comes off it)
    public float P0, P1;                // extras, by kind: Peek: how far out, from and to. Hop: arc height in squirrel units, P1 1 = starts from a run, 2 = ends in a run, 3 = both.
                                        // Run: the bound length (pixels). Dig: scrabbles a second (0 = just sniffing), P1 = 1 with earth flying.
                                        // Fracas: fade in and fade out seconds. Rise: P0 1 = rising to sit, -1 = sinking from a sit to the crouch. Chatter: P0 = which squirrel (0 or 1).
                                        // Bird: P0 0 stand, 1 look, 2 peck, 3 hop toward To; P1 = which bird (0 junco, 1 sparrow). BirdFly: P1 = which bird, P0 = extra arc height (U).
                                        // MousePeek: From = the hole, P0 to P1 how far out, P1 of the stencil: Rail true = through the jack's holes. Mouse: Rail true = through the jack's holes.
    public bool[]? Stencil;             // an optional stencil of the segment's own (the jack-o'-lantern's carved holes)
    public int Near = -1;               // the nearness to stamp with, when it is not the feet's own (a bird on a pumpkin: that pumpkin's plus one)
    public float Dur => T1 - T0;
}

internal struct Sheet
{
    public Sprite[] Pages;
    public int W, H, Ox, By;            // the sheet, and where the feet's origin is on it
}

/// <summary>Every actor's pictures, shared by all the stories, and how a segment is drawn from them.</summary>
internal sealed class Cast
{
    // 48 run pages: at 16, a 132 px bound at 4.9 px a frame put the planted paw 5 px forward and back again every page (the animator's "creep").
    public const int RunPages = 48, HopPages = 10, TurnPages = 10, RisePages = 5, DigPages = 8, MousePages = 8, CloudPages = 12, PeekPages = 4, AirPages = 8, MountPages = 12, ApplePages = 8, DazedPages = 24, LimbTurns = 8;   // 2026-11-09: the turn 3 to 10 pages and the mount 4 to 12, each over twice the time ("the squirrel 2d flipping and 90 turns")   // 24 dazed pages: at 8 the slow loll held each page nine frames and stepped
    public const float BoundLen = 2.2f;                 // the bound the run pages are painted at, in squirrel units: every run and climb is paged at it
    public const float Speed = 0.27f;                  // bounding, U per second (the fence squirrel's own pace)
    public const float ClimbSpeed = 0.34f;             // up a trunk: faster than on the ground, in body lengths a second
    public const float MouseSpeed = 0.19f;
    public const float HopFor = 0.36f, TurnFor = 0.42f, RiseFor = 0.22f, MountFor = 0.40f;

    public readonly ThanksgivingScenery S;
    public readonly Noticeboard Board;                 // where a story tells the residents what is going on (a scuffle, a squirrel bolting)
    public readonly float K, M, B;                     // the squirrel, mouse and bird units, in pixels
    public Sheet[] Run = new Sheet[2];                 // [acorn]
    public Sheet[] Hop = new Sheet[2];
    public Sheet[] Up = new Sheet[2];                  // climbing, head up: [acorn]
    public Sheet Down;                                 // climbing, head first down
    public Sheet[] Mount = new Sheet[2];               // pitching up onto the bark from a level stance facing right: [acorn]; played backward it is the dismount onto the rail
    public Sheet[] MountDown = new Sheet[2];           // the end of a head-first descent: the body pitching from head-down to level, drawn mirrored like the descent, so it ends facing LEFT
    public Sheet[] Turn = new Sheet[2];                // [acorn]: a turn with the nut held to the chest keeps it in view (the turn used to drop it for twelve frames)
    public Sheet[] Rise = new Sheet[2];
    public Sheet[,] Sit = new Sheet[2, 4];             // [acorn, tail flick pose]
    public Sheet[] LookUp = new Sheet[2];
    public Sheet[] Dig = new Sheet[2];
    public Sheet[,] Chatter = new Sheet[4, 2];         // [tail snap, paw]
    public Sheet Dazed;
    public Sheet Peek, Mouse, MouseNut, MouseStill, MouseStillNut, MouseHead, Cloud, Air;
    public const int AppleSizes = 4;
    public Sheet[] Apple = new Sheet[AppleSizes];       // [size]: 0 full, 3 the far size by the fence
    public Sheet[] BirdStand = new Sheet[2], BirdLook = new Sheet[2], BirdPeck = new Sheet[2], BirdHop = new Sheet[2], BirdFly = new Sheet[2];
    public Sprite[,] Limb = new Sprite[4, LimbTurns];  // a paw, a hind foot, an ear, a tail tip, for the scuffle, each at eight angles round the circle
    public Sprite Acorn = null!, Mound = null!, Hole = null!, Speck = null!, Tuft = null!, Star = null!;
    public volatile bool Ready;

    public Cast(ThanksgivingScenery s, Noticeboard board)
    {
        S = s;
        Board = board;
        K = Math.Max(9f, s.U * 0.030f);   // the near ground's scale: a pumpkin of 0.4 m is about 0.05 U, so a squirrel of 0.25 m is 0.030 U (at 0.055 it was "too big"), a mouse 0.55 of that, a sparrow 0.020
        M = K * 0.55f;
        B = Math.Max(7f, s.U * 0.020f);
        // The pictures take a moment to paint (a few hundred pages); done on a side thread so the scene starts at once.
        Task.Run(() => { try { Build(); } catch { Ready = false; return; } Ready = true; });
    }

    public static Sheet MakeSheet(int n, int w, int h, int ox, int by, Func<int, Sprite> page)
    {
        var sh = new Sheet { Pages = new Sprite[n], W = w, H = h, Ox = ox, By = by };
        for (int i = 0; i < n; i++) sh.Pages[i] = page(i);
        return sh;
    }

    private void Build()
    {
        float k = K;
        int w = (int)(2.55f * k) + 6, h = (int)(1.9f * k) + 6, ox = (int)(1.75f * k) + 3, by = h - 4;
        float bound = BoundLen;
        Sheet Ground(int n, Func<int, SquirrelArt.Pose> pose) => MakeSheet(n, w, h, ox, by, i => SquirrelArt.Page(pose(i), k, w, h, ox, by));
        int cw = (int)(1.9f * k) + 6, ch = (int)(2.55f * k) + 6;
        int sq = (int)(4.0f * k) + 6, so = sq / 2;
        for (int a = 0; a < 2; a++)
        {
            int nut = a;
            Run[a] = Ground(RunPages, i => { var q = SquirrelArt.Bound(i / (float)RunPages, bound); q.Acorn = nut; return q; });
            Hop[a] = Ground(HopPages, i => { var q = SquirrelArt.Hop((i + 0.5f) / HopPages); q.Acorn = nut == 1 ? (q.PawsUp ? 2 : 1) : 0; return q; });
            Up[a] = MakeSheet(RunPages, cw, ch, (int)(1.9f * k) + 3, (int)(0.8f * k) + 3, i => { var q = SquirrelArt.Bound(i / (float)RunPages, bound, cling: true); q.Acorn = nut; q.Rotate = -90f; return SquirrelArt.Page(q, k, cw, ch, (int)(1.9f * k) + 3, (int)(0.8f * k) + 3); });
            // The mount: the gathered crouch (the pose the rise pages begin from, so a sink runs straight into it) pitched up about
            // the feet over three pages, and the clinging climb pose on the last. (Built from the mid-air leap it lifted the body a
            // third of a length off the rail in one frame.) Played backward it is the dismount, ending in the same crouch.
            // The mount (since 2026-11-09, as PatchLife's): the CLINGING body itself, the pose the climb's first page has, swung about
            // its hind feet from level to upright, eased, twelve pages over 0.4 s. (The first version swung a sitting squirrel up in
            // three jumps and switched to the cling on the last page: a card flipped up.) Played backward it is the dismount.
            Mount[a] = MakeSheet(MountPages, sq, sq, so, so, i =>
            {
                float f = (i + 1) / (float)MountPages, e = f * f * (3f - 2f * f);
                var q = SquirrelArt.Bound(0f, bound, cling: true); q.Acorn = nut; q.Rotate = -90f * e; return SquirrelArt.Page(q, k, sq, sq, so, so);
            });
            // The end of a head-first descent: the clinging pose pitching from head-down to level (mirrored at the stamp).
            MountDown[a] = MakeSheet(MountPages, sq, sq, so, so, i =>
            {
                float f = (i + 1) / (float)MountPages, e = f * f * (3f - 2f * f);
                var q = SquirrelArt.Bound(0f, bound, cling: true); q.Acorn = nut; q.Rotate = 90f * (1f - e); return SquirrelArt.Page(q, k, sq, sq, so, so);
            });
            Turn[a] = Ground(TurnPages, i => { var q = SquirrelArt.Turn(0.5f * (i + 1) / TurnPages, nut == 1); q.Acorn = nut == 1 ? 2 : 0; return q; });   // the profile coming round to the front view, ten steps; the caller plays it back mirrored
            Rise[a] = Ground(RisePages, i => { var q = SquirrelArt.Sit(0.2f + 0.8f * i / (RisePages - 1), 0, false, nut == 1); q.Acorn = nut == 1 ? (q.PawsUp ? 2 : 1) : 0; return q; });
            for (int tail = 0; tail < 4; tail++)
            {
                int tl = tail;
                Sit[a, tail] = Ground(1, _ => { var q = SquirrelArt.Sit(1f, tl, false, nut == 1); q.Acorn = nut == 1 ? 2 : 0; return q; });
            }
            LookUp[a] = Ground(1, _ => { var q = SquirrelArt.Sit(1f, 0, true, nut == 1); q.Acorn = nut == 1 ? 2 : 0; return q; });
            Dig[a] = Ground(DigPages, i => { var q = SquirrelArt.Dig(i / (float)DigPages); q.Acorn = nut; return q; });
        }
        Down = MakeSheet(RunPages, cw, ch, 3, (int)(1.75f * k) + 3, i => { var q = SquirrelArt.Bound(i / (float)RunPages, bound, cling: true); q.Rotate = 90f; return SquirrelArt.Page(q, k, cw, ch, 3, (int)(1.75f * k) + 3); });
        for (int snap = 0; snap < 4; snap++)
            for (int paw = 0; paw < 2; paw++)
            {
                int sn = snap, pw = paw;
                Chatter[snap, paw] = Ground(1, _ => SquirrelArt.Chatter(sn, pw));
            }
        Dazed = Ground(DazedPages, i => SquirrelArt.Dazed(15f * MathF.Sin(MathF.Tau * i / DazedPages)));   // a slow loll through 15 degrees each way (three pages of 5 degrees at 3 Hz was a twitch)
        {
            int pw2 = (int)(0.9f * k) + 4, ph = (int)(0.8f * k) + 4, pox = pw2 / 2, pby = (int)(0.72f * k) + 2;
            Peek = MakeSheet(PeekPages + 1, pw2, ph, pox, pby, i => Squirrel.PaintPeek(k, pw2, ph, pox, pby, i < PeekPages ? (i + 1) / (float)PeekPages : 1f, i == PeekPages));
        }

        Acorn = Squirrel.PaintAcorn(k * 1.6f, 90f);
        Air = MakeSheet(AirPages, 1, 1, 0, 0, i => Squirrel.PaintAcorn(k * 1.6f, 360f * i / AirPages));

        float m = M;
        int mw = (int)(1.9f * m) + 4, mh = (int)(0.95f * m) + 4, mox = (int)(1.25f * m) + 2, mby = mh - 2;
        Sprite MousePage(float phase, bool nut, int twitch) => Sprite.Paint(mw, mh, g => { g.TranslateTransform(mox, mby); g.ScaleTransform(m, -m); MouseArt.Paint(g, phase, nut, twitch); });
        Mouse = MakeSheet(MousePages, mw, mh, mox, mby, i => MousePage(i / (float)MousePages, false, 0));
        MouseNut = MakeSheet(MousePages, mw, mh, mox, mby, i => MousePage(i / (float)MousePages, true, 0));
        MouseStill = MakeSheet(2, mw, mh, mox, mby, i => MousePage(0f, false, i + 1));
        MouseStillNut = MakeSheet(2, mw, mh, mox, mby, i => MousePage(0f, true, i + 1));
        // The mouse's head alone, for peeking out of a hole: the sheet's origin is the hole's middle; the head rises above it.
        {
            int hw = (int)(0.8f * m) + 4, hh = (int)(0.9f * m) + 4, hox = hw / 2, hby = (int)(0.6f * m) + 2;
            MouseHead = MakeSheet(4, hw, hh, hox, hby, i => Sprite.Paint(hw, hh, g =>
            {
                g.TranslateTransform(hox, hby);
                g.ScaleTransform(m, -m);
                float rise = -0.35f + 0.45f * (i + 1) / 4f;
                using (var clip = new GraphicsPath()) { clip.AddRectangle(new RectangleF(-2f, -0.02f, 4f, 3f)); g.SetClip(clip); }
                Color fur = Color.FromArgb(150, 126, 102), pink = Color.FromArgb(226, 156, 150), rim = Color.FromArgb(80, 60, 52);
                using (var head = new SolidBrush(fur)) g.FillEllipse(head, -0.18f, rise - 0.16f, 0.36f, 0.32f);
                using (var er = new SolidBrush(rim)) { g.FillEllipse(er, -0.26f, rise + 0.05f, 0.24f, 0.24f); g.FillEllipse(er, 0.02f, rise + 0.05f, 0.24f, 0.24f); }
                using (var ear = new SolidBrush(fur)) { g.FillEllipse(ear, -0.24f, rise + 0.07f, 0.20f, 0.20f); g.FillEllipse(ear, 0.04f, rise + 0.07f, 0.20f, 0.20f); }
                using (var inner = new SolidBrush(pink)) { g.FillEllipse(inner, -0.20f, rise + 0.10f, 0.12f, 0.13f); g.FillEllipse(inner, 0.08f, rise + 0.10f, 0.12f, 0.13f); }
                using (var eye = new SolidBrush(Color.FromArgb(24, 18, 18))) { g.FillEllipse(eye, -0.10f, rise - 0.02f, 0.06f, 0.06f); g.FillEllipse(eye, 0.04f, rise - 0.02f, 0.06f, 0.06f); }
                using (var np = new SolidBrush(pink)) g.FillEllipse(np, -0.035f, rise - 0.14f, 0.07f, 0.06f);
                using (var wh = new Pen(Color.FromArgb(140, 230, 226, 230), 0.015f))
                    for (int j = -1; j <= 1; j++) { g.DrawLine(wh, -0.03f, rise - 0.11f, -0.26f, rise - 0.12f + j * 0.06f); g.DrawLine(wh, 0.03f, rise - 0.11f, 0.26f, rise - 0.12f + j * 0.06f); }
            }));
        }

        float b = B;
        int bw = (int)(1.6f * b) + 4, bh = (int)(1.1f * b) + 4, box = (int)(0.9f * b) + 2, bby = bh - 2;
        for (int kind = 0; kind < 2; kind++)
        {
            int kk = kind;
            BirdStand[kind] = MakeSheet(1, bw, bh, box, bby, _ => BirdArt.Page(BirdArt.Pose.Stand, kk, b, bw, bh, box, bby));
            BirdLook[kind] = MakeSheet(1, bw, bh, box, bby, _ => BirdArt.Page(BirdArt.Pose.Look, kk, b, bw, bh, box, bby));
            BirdPeck[kind] = MakeSheet(1, bw, bh, box, bby, _ => BirdArt.Page(BirdArt.Pose.Peck, kk, b, bw, bh, box, bby));
            BirdHop[kind] = MakeSheet(3, bw, bh, box, bby, i => BirdArt.Page(i == 0 ? BirdArt.Pose.HopCrouch : i == 1 ? BirdArt.Pose.HopUp : BirdArt.Pose.HopLand, kk, b, bw, bh, box, bby));
            BirdFly[kind] = MakeSheet(6, bw, bh, box, bby, i => BirdArt.Page(BirdArt.Pose.Fly, kk, b, bw, bh, box, bby, i));
        }

        int fw = (int)(3.0f * k) + 4, fh = (int)(2.4f * k) + 4;
        Cloud = MakeSheet(CloudPages, fw, fh, fw / 2, fh - 2, i => PaintCloud(fw, fh, i / (float)CloudPages));
        int lw = (int)(0.9f * k) + 4;
        for (int i = 0; i < 4; i++)
            for (int turn = 0; turn < LimbTurns; turn++)
            {
                int kind = i; float deg = 360f * turn / LimbTurns;
                Limb[i, turn] = Sprite.Paint(lw, lw, g => PaintLimb(g, lw, kind, deg));
            }
        int tw = Math.Max(4, (int)(k * 0.12f));
        Tuft = Sprite.Paint(tw, tw, g => { using var br = new SolidBrush(SquirrelArt.TailHair); g.FillEllipse(br, 0.5f, tw * 0.25f, tw - 1f, tw * 0.5f); });
        int stw = Math.Max(6, (int)(k * 0.16f));
        Star = Sprite.Paint(stw, stw, g =>
        {
            using var br = new SolidBrush(Color.FromArgb(250, 236, 180));
            float c = stw / 2f, r = stw / 2f - 0.5f, r2 = r * 0.35f;
            var pts = new PointF[8];
            for (int i = 0; i < 8; i++) { float a = i * MathF.PI / 4 - MathF.PI / 2, rr = i % 2 == 0 ? r : r2; pts[i] = new PointF(c + rr * MathF.Cos(a), c + rr * MathF.Sin(a)); }
            g.FillPolygon(br, pts);
        });

        int gw = (int)(0.7f * k) + 4, gh = (int)(0.3f * k) + 4;
        Mound = Sprite.Paint(gw, gh, g =>
        {
            using var earth = new SolidBrush(Color.FromArgb(104, 66, 34));
            g.FillEllipse(earth, 2, gh * 0.30f, gw - 4, gh * 0.75f);
            using var crumb = new SolidBrush(Color.FromArgb(150, 104, 58));
            g.FillEllipse(crumb, gw * 0.30f, gh * 0.30f, gw * 0.28f, gh * 0.28f);
            g.FillEllipse(crumb, gw * 0.55f, gh * 0.42f, gw * 0.18f, gh * 0.22f);
        });
        Hole = Sprite.Paint(gw, gh, g =>
        {
            using var rim = new SolidBrush(Color.FromArgb(118, 76, 40));
            g.FillEllipse(rim, 1, gh * 0.25f, gw - 2, gh * 0.75f);
            using var dark = new SolidBrush(Color.FromArgb(38, 22, 12));
            g.FillEllipse(dark, gw * 0.22f, gh * 0.42f, gw * 0.56f, gh * 0.40f);
        });
        int sp = Math.Max(4, (int)(k * 0.09f));
        Speck = Sprite.Paint(sp, sp, g => { using var br = new SolidBrush(Color.FromArgb(92, 56, 28)); g.FillEllipse(br, 0.5f, 0.5f, sp - 1f, sp - 1f); });

        // An apple, eight pages of a roll: red with a paler cheek, a stem, a leaf. Four sizes, full on the near ground down
        // to about half in the field by the fence (things further off are smaller; the roll under the rail steps through them).
        for (int size = 0; size < AppleSizes; size++)
        {
            float ar = Math.Max(2.5f, S.U * 0.013f * (1f - 0.15f * size));
            int asz = (int)(ar * 2.8f) + 4;
            Apple[size] = MakeSheet(ApplePages, asz, asz, asz / 2, asz / 2, i => Sprite.Paint(asz, asz, g =>
            {
                g.TranslateTransform(asz / 2f, asz / 2f);
                g.RotateTransform(360f * i / ApplePages);
                using (var red = new SolidBrush(Color.FromArgb(196, 36, 32))) g.FillEllipse(red, -ar, -ar * 0.95f, ar * 2, ar * 1.9f);
                using (var cheek = new SolidBrush(Color.FromArgb(120, 240, 120, 90))) g.FillEllipse(cheek, -ar * 0.6f, -ar * 0.65f, ar * 0.7f, ar * 0.9f);
                using (var stem = new Pen(Color.FromArgb(80, 52, 28), Math.Max(1f, ar * 0.18f)) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawLine(stem, 0, -ar * 0.8f, ar * 0.1f, -ar * 1.25f);
                using (var leaf = new SolidBrush(Color.FromArgb(84, 130, 50))) g.FillEllipse(leaf, ar * 0.05f, -ar * 1.3f, ar * 0.55f, ar * 0.28f);
            }));
        }
    }

    /// <summary>
    /// The scuffle at one moment (t 0 to 1 of a third of a second, which is one hop of the whole cloud): a dozen
    /// puffs of dust in a pale, cooler tone than the ground, each pulsing at its own rate and jittering, the whole
    /// ball hopping and squashing as it lands, with two bushy tails sticking out of it and a few bits of grass
    /// flying off. Twelve pages played at 36 a second boil instead of ticking round.
    /// </summary>
    private Sprite PaintCloud(int w, int h, float t) =>
        Sprite.Paint(w, h, g =>
        {
            float k = K;
            float hop = 0.14f * MathF.Abs(MathF.Sin(MathF.PI * t));
            float squash = 1f + 0.10f * MathF.Max(0f, 1f - MathF.Abs(MathF.Sin(MathF.PI * t)) * 6f);
            g.TranslateTransform(w / 2f, h - 2 - (0.62f + hop) * k);
            PointF Polar(float deg, float r) { float a = deg * MathF.PI / 180f; return new PointF(r * k * MathF.Cos(a) * squash, r * k * MathF.Sin(a) / squash); }
            // Two tails thrashing out of the ball: each wags through forty degrees (at a full turn a page they spun like a pinwheel),
            // the tufts all the core's colour as on the real tail (light and dark by turns banded it).
            foreach (float deg in (float[])[25f + 20f * MathF.Sin(MathF.Tau * t), 205f - 20f * MathF.Sin(MathF.Tau * t + 1f)])
            {
                PointF a = Polar(deg, 0.30f), b = Polar(deg + 12f, 0.72f), c = Polar(deg - 18f, 1.0f);
                using var hair = new Pen(SquirrelArt.TailHair, k * 0.20f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                using var core = new Pen(SquirrelArt.TailCore, k * 0.11f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                g.DrawBezier(hair, a, b, Polar(deg - 5f, 0.9f), c);
                g.DrawBezier(core, a, b, Polar(deg - 5f, 0.9f), c);
                using var tuft = new SolidBrush(SquirrelArt.TailCore);
                for (int i = 0; i < 5; i++)
                {
                    PointF p = Polar(deg + 12f - 30f * (i / 4f), 0.5f + 0.5f * (i / 4f));
                    g.FillEllipse(tuft, p.X - k * 0.07f, p.Y - k * 0.07f, k * 0.14f, k * 0.14f);
                }
            }
            (float Deg, float R, float Sz, float F, float Ph)[] puffs =
            [
                (0, 0, 0.46f, 6, 0.0f), (30, 0.34f, 0.30f, 9, 0.2f), (75, 0.40f, 0.26f, 6, 0.5f), (120, 0.36f, 0.30f, 9, 0.8f), (165, 0.42f, 0.26f, 6, 0.1f),
                (210, 0.36f, 0.32f, 9, 0.4f), (250, 0.40f, 0.26f, 6, 0.7f), (290, 0.34f, 0.30f, 9, 0.9f), (330, 0.40f, 0.28f, 6, 0.3f),
                (55, 0.62f, 0.18f, 9, 0.6f), (185, 0.62f, 0.18f, 6, 0.25f), (300, 0.60f, 0.17f, 9, 0.85f),
            ];
            foreach (var p in puffs)
            {
                float pulse = 1f + 0.2f * MathF.Sin(MathF.Tau * (p.F / 3f * t + p.Ph));
                float jx = 0.08f * MathF.Sin(MathF.Tau * (2 * t + p.Ph * 3)), jy = 0.06f * MathF.Cos(MathF.Tau * (2 * t + p.Ph * 5));
                PointF c = Polar(p.Deg, p.R);
                c = new PointF(c.X + jx * k, c.Y + jy * k);
                float s = p.Sz * pulse * k;
                using var lo = new SolidBrush(Color.FromArgb(236, 214, 188, 150));
                g.FillEllipse(lo, c.X - s, c.Y - s * 0.9f, s * 2, s * 1.9f);
                using var hi = new SolidBrush(Color.FromArgb(220, 240, 224, 196));
                g.FillEllipse(hi, c.X - s * 0.8f, c.Y - s * 1.0f, s * 1.6f, s * 1.3f);
            }
            using (var grass = new Pen(Color.FromArgb(214, 166, 84), Math.Max(1f, k * 0.025f)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                foreach (float deg in (float[])[65f, 160f, 275f, 330f])
                {
                    PointF a = Polar(deg - 200f * t, 1.0f), b = Polar(deg - 200f * t + 8f, 1.2f);
                    g.DrawLine(grass, a, b);
                }
        });

    /// <summary>A bit of squirrel that pops out of the dust: 0 a paw with spread fingers, 1 a long hind foot, 2 an ear, 3 a tail tip. Painted from the sheet's centre pointing "deg" degrees round from the right (anticlockwise on the screen).</summary>
    private void PaintLimb(Graphics g, int size, int kind, float deg)
    {
        float k = K;
        g.TranslateTransform(size / 2f, size / 2f);
        g.RotateTransform(-deg);
        g.ScaleTransform(k, -k);
        using var limb = new SolidBrush(SquirrelArt.LimbNear);
        using var fur = new SolidBrush(SquirrelArt.Fur);
        switch (kind)
        {
            case 0:
                g.FillEllipse(limb, 0f, -0.04f, 0.26f, 0.08f);
                g.FillEllipse(limb, 0.22f, -0.06f, 0.10f, 0.12f);
                using (var finger = new Pen(SquirrelArt.LimbNear, 0.03f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                    foreach (float a in (float[])[-0.5f, -0.17f, 0.17f, 0.5f]) g.DrawLine(finger, 0.30f, 0f, 0.30f + 0.10f * MathF.Cos(a), 0.10f * MathF.Sin(a));
                break;
            case 1:
                g.FillEllipse(limb, 0f, -0.045f, 0.24f, 0.09f);
                g.FillEllipse(limb, 0.20f, -0.035f, 0.22f, 0.07f);
                break;
            case 2:
                g.FillEllipse(fur, 0.02f, -0.07f, 0.16f, 0.14f);
                using (var inner = new SolidBrush(Color.FromArgb(190, 140, 120))) g.FillEllipse(inner, 0.07f, -0.04f, 0.09f, 0.08f);
                using (var tuft = new SolidBrush(SquirrelArt.Under)) g.FillEllipse(tuft, 0.16f, -0.03f, 0.06f, 0.06f);
                break;
            default:
                using (var hair = new Pen(SquirrelArt.TailHair, 0.16f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                    g.DrawBezier(hair, new PointF(0, 0), new PointF(0.15f, 0.05f), new PointF(0.3f, 0.12f), new PointF(0.42f, 0.06f));
                using (var core = new Pen(SquirrelArt.TailCore, 0.08f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                    g.DrawBezier(core, new PointF(0, 0), new PointF(0.15f, 0.05f), new PointF(0.3f, 0.12f), new PointF(0.42f, 0.06f));
                break;
        }
    }

    // ================================================================ drawing a segment

    public static float Smooth(float x) { x = Math.Clamp(x, 0f, 1f); return x * x * (3 - 2 * x); }
    public float WalkY(float x) => S.WalkY(x);
    public float RailAt(float x) => Squirrel.RailAt(S, x);

    /// <summary>
    /// Stamps a page with its feet at (feetX, feetY). Through the stencil if one is given (the rail and the trunks,
    /// or the jack-o'-lantern's holes), and always against the near ground's depth picture with the nearness of the
    /// feet (or the nearness given), so a pumpkin whose foot is lower on the screen hides the actor where they overlap.
    /// </summary>
    public void Stamp(FrameBuffer fb, in Sheet sh, Sprite page, float feetX, float feetY, bool mirror, bool[]? stencil, float opacity = 1f, int nearness = -1)
    {
        int left = (int)MathF.Round(feetX) - (mirror ? sh.W - sh.Ox : sh.Ox);
        page.Draw(fb, left, (int)MathF.Round(feetY) - sh.By, opacity, stencil, mirror, S.NearDepth, (byte)(nearness < 0 ? S.Nearness(feetY) : nearness));
    }

    /// <summary>Draws one segment of one actor at story time t. The Fracas cloud's pops, tufts and stars are the story's own (see AcornCaper).</summary>
    public void DrawSeg(FrameBuffer fb, Seg seg, float t)
    {
        float k = K, u = (t - seg.T0) / Math.Max(0.001f, seg.Dur);
        bool mirror = seg.Dir < 0;
        int nut = seg.Acorn ? 1 : 0;
        bool[]? props = S.OpenOfProps;
        int near = seg.Near;

        switch (seg.Kind)
        {
            case Kind.Still:
                break;
            case Kind.Peek:
            {
                float outBy = seg.P0 + (seg.P1 - seg.P0) * Smooth(u);
                bool blink = seg.P0 >= 1f && (t - seg.T0) % 1.3f < 0.12f;
                int page = blink ? PeekPages : Math.Clamp((int)MathF.Ceiling(outBy * PeekPages) - 1, 0, PeekPages - 1);
                Stamp(fb, Peek, Peek.Pages[page], seg.From.X, seg.From.Y, mirror, null);
                break;
            }
            case Kind.Hop:
            {
                // The feet stay put through the crouch and the push, and again through the landing and the settle: only the
                // FLIGHT pages travel, on a parabola whose height is P0 squirrel units. (Spread over the whole hop, the crouch
                // was already in the air and the landing squash played above the ground.) A drop is the same parabola with
                // the far end lower, so it speeds up as it falls.
                int flags = (int)seg.P1;
                float k0 = (flags & 1) != 0 ? SquirrelArt.HopCrouch : 0f, k1 = (flags & 2) != 0 ? SquirrelArt.HopLand : 1f;
                float kk = k0 + (k1 - k0) * u;
                float fl = Math.Clamp((kk - SquirrelArt.HopPush) / (SquirrelArt.HopFlight - SquirrelArt.HopPush), 0f, 1f);
                float x = seg.From.X + (seg.To.X - seg.From.X) * fl;
                float y = seg.From.Y + (seg.To.Y - seg.From.Y) * fl - seg.P0 * k * 4 * fl * (1 - fl);
                int page = Math.Clamp((int)(kk * HopPages), 0, HopPages - 1);
                Stamp(fb, Hop[nut], Hop[nut].Pages[page], x, y, mirror, null, 1f, S.Nearness(Math.Max(seg.From.Y, seg.To.Y)));
                break;
            }
            case Kind.Turn:
            {
                int toDir = seg.Dir, fromDir = -seg.Dir;
                int page = u < 0.5f ? Math.Min(TurnPages - 1, (int)(u * 2 * TurnPages)) : Math.Min(TurnPages - 1, (int)((1 - u) * 2 * TurnPages));
                bool m = u < 0.5f ? fromDir < 0 : toDir < 0;
                Stamp(fb, Turn[nut], Turn[nut].Pages[page], seg.From.X, seg.From.Y, m, null);
                break;
            }
            case Kind.Rise:
            {
                float kk = seg.P0 > 0 ? u : 1 - u;
                int page = Math.Clamp((int)(kk * RisePages), 0, RisePages - 1);
                Stamp(fb, Rise[nut], Rise[nut].Pages[page], seg.From.X, seg.From.Y, mirror, null);
                break;
            }
            case Kind.Run:
            {
                if (seg.From == seg.To)
                {
                    // A stand at the end of a run (the lunge's last frames under the dust): the gathered crouch, tail up inside
                    // the cloud, fading out over the segment so nothing is ever uncovered (a frozen run page with the tail
                    // streaming flat vanished in one frame outside the cloud).
                    Stamp(fb, Rise[nut], Rise[nut].Pages[0], seg.From.X, seg.From.Y, mirror, null, 1f - u);
                    break;
                }
                float dist = MathF.Abs(seg.To.X - seg.From.X) * u;
                float x = seg.From.X + (seg.To.X - seg.From.X) * u;
                float y = seg.Rail ? RailAt(x) - 1.5f : seg.From.Y + (seg.To.Y - seg.From.Y) * u;
                float cyc = dist / Math.Max(1f, seg.P0);
                int page = (int)((cyc - MathF.Floor(cyc)) * RunPages) % RunPages;
                Stamp(fb, Run[nut], Run[nut].Pages[page], x, y, mirror, seg.Rail ? props : null);
                break;
            }
            case Kind.Mount:
            {
                // Never mirrored: the climb pages are not, and a mirrored mount put the body on the other side of the trunk
                // from the climb's first page. A mount up (Up) starts facing right; a dismount (not Up) ends facing right; a
                // dismount from a head-first descent (P0 1) uses its own pages, mirrored like the descent, and ends facing LEFT.
                // The script builders put a Turn before or after for the other direction.
                if (seg.P0 > 0f)
                {
                    int page = Math.Clamp((int)(u * MountPages), 0, MountPages - 1);
                    Stamp(fb, MountDown[nut], MountDown[nut].Pages[page], seg.From.X, seg.From.Y, true, null);
                    break;
                }
                float kk = seg.Up ? u : 1 - u;
                {
                    int page = Math.Clamp((int)(kk * MountPages), 0, MountPages - 1);
                    Stamp(fb, Mount[nut], Mount[nut].Pages[page], seg.From.X, seg.From.Y, false, null);
                }
                break;
            }
            case Kind.Climb:
            {
                float x = seg.From.X + (seg.To.X - seg.From.X) * u, y = seg.From.Y + (seg.To.Y - seg.From.Y) * u;
                float cyc = Dist(seg.From, seg.To) * u / (BoundLen * k);                    // paged at the bound the art was painted at (at 1.6 the paws slipped 9 px a contact)
                int page = (int)((cyc - MathF.Floor(cyc)) * RunPages) % RunPages;
                if (seg.Up) Stamp(fb, Up[nut], Up[nut].Pages[page], x, y, false, props);
                else Stamp(fb, Down, Down.Pages[page], x, y, true, props);
                break;
            }
            case Kind.Sit:
            {
                float s = t - seg.T0;
                float ft = s % 1.5f;
                int tail = ft < 0.4f ? new[] { 1, 2, 3, 2, 1 }[Math.Min(4, (int)(ft / 0.4f * 5))] : 0;
                Sheet sh = seg.Look ? LookUp[nut] : Sit[nut, tail];
                Stamp(fb, sh, sh.Pages[0], seg.From.X, seg.From.Y, mirror, null);
                break;
            }
            case Kind.Chatter:
            {
                float s = t - seg.T0 + seg.P0 * 0.23f;
                int f = (int)(s * 60);
                int cyc = f % 14;
                int snap = cyc < 6 ? new[] { 1, 2, 3, 3, 2, 1 }[cyc] : 0;
                int paw = (f / 8) % 2;
                float shudder = MathF.Round(MathF.Sin(s * 15f * MathF.Tau));
                Sheet sh = Chatter[snap, paw];
                Stamp(fb, sh, sh.Pages[0], seg.From.X + shudder, seg.From.Y, mirror, null);
                break;
            }
            case Kind.Dazed:
            {
                // A slow loll at 0.8 a second through the eight pages; the pair fade in under the thinning dust (they used to pop in at full strength half outside it).
                float s = t - seg.T0;
                int page = (int)(s * 0.8f * DazedPages) % DazedPages;
                Stamp(fb, Dazed, Dazed.Pages[page], seg.From.X, seg.From.Y, mirror, null, Smooth(s / 0.3f));
                break;
            }
            case Kind.Dig:
            {
                float s = t - seg.T0;
                int page = seg.P0 <= 0f ? 0 : (int)(s * seg.P0 * DigPages) % DigPages;
                Stamp(fb, Dig[nut], Dig[nut].Pages[page], seg.From.X, seg.From.Y, mirror, seg.Rail ? props : null);
                if (seg.P1 > 0f)
                {
                    float px = seg.From.X + seg.Dir * 0.5f * k, py = seg.From.Y;
                    for (int i = 0; i < 14; i++)
                    {
                        float p = (s * seg.P0 * 0.4f + i / 14f) % 1f;
                        float reach = (1.3f + 0.35f * (i % 4)) * k, lift = (0.9f + 0.2f * (i % 3)) * k;
                        float x = px - seg.Dir * reach * p, y = py - lift * 4 * p * (1 - p) + 0.02f * k;
                        Speck.DrawCentered(fb, x, y, 1 - Smooth((p - 0.8f) / 0.2f), null, false, S.NearDepth, S.Nearness(py));
                    }
                }
                break;
            }
            case Kind.Mouse:
            {
                float x = seg.From.X + (seg.To.X - seg.From.X) * u, y = seg.From.Y + (seg.To.Y - seg.From.Y) * u;
                bool still = seg.From == seg.To;
                float s = t - seg.T0;
                Sheet sh;
                int page;
                if (still) { sh = seg.Acorn ? MouseStillNut : MouseStill; page = (int)(s * 12) % 2; }
                else
                {
                    float dist = Dist(seg.From, seg.To) * u;
                    float stride = 0.8f * M;
                    float cyc = dist / stride;
                    sh = seg.Acorn ? MouseNut : Mouse; page = (int)((cyc - MathF.Floor(cyc)) * MousePages) % MousePages;
                }
                Stamp(fb, sh, sh.Pages[page], x, y, mirror, seg.Stencil);
                break;
            }
            case Kind.MousePeek:
            {
                // The mouse's head rising out of a hole (the jack-o'-lantern's eye or mouth), shown only through the carved holes.
                float outBy = seg.P0 + (seg.P1 - seg.P0) * Smooth(u);
                int page = Math.Clamp((int)MathF.Ceiling(outBy * 4) - 1, 0, 3);
                Stamp(fb, MouseHead, MouseHead.Pages[page], seg.From.X, seg.From.Y, mirror, seg.Stencil, 1f, 254);
                break;
            }
            case Kind.Fracas:
            {
                float s = t - seg.T0;
                float opacity = Smooth(s / seg.P0) * Smooth((seg.Dur - s) / seg.P1);
                float spread = 1f + 0.5f * Smooth(1 - (seg.Dur - s) / seg.P1);
                float x = seg.From.X + 0.3f * k * MathF.Sin(s * 6.1f), y = seg.From.Y;
                int page = (int)(s * 36) % CloudPages;
                Stamp(fb, Cloud, Cloud.Pages[page], x, y, false, null, opacity / spread);
                break;
            }
            case Kind.Bird:
            {
                int kind = (int)seg.P1;
                float s = t - seg.T0;
                float x = seg.From.X + (seg.To.X - seg.From.X) * u, y = seg.From.Y + (seg.To.Y - seg.From.Y) * u;
                if (seg.P0 >= 3f)
                {
                    float hop = (s * 4f) % 1f;
                    int page = hop < 0.2f ? 0 : hop < 0.7f ? 1 : 2;
                    float lift = hop >= 0.2f && hop < 0.7f ? 0.25f * B * MathF.Sin((hop - 0.2f) / 0.5f * MathF.PI) : 0f;
                    Stamp(fb, BirdHop[kind], BirdHop[kind].Pages[page], x, y - lift, mirror, null, 1f, near < 0 ? S.Nearness(y) : near);
                }
                else if (seg.P0 >= 2f) { Sheet sh = (s % 0.5f) < 0.3f ? BirdPeck[kind] : BirdStand[kind]; Stamp(fb, sh, sh.Pages[0], x, y, mirror, null, 1f, near); }
                else if (seg.P0 >= 1f) Stamp(fb, BirdLook[kind], BirdLook[kind].Pages[0], x, y, mirror, null, 1f, near);
                else Stamp(fb, BirdStand[kind], BirdStand[kind].Pages[0], x, y, mirror, null, 1f, near);
                break;
            }
            case Kind.BirdFly:
            {
                int kind = (int)seg.P1;
                float s = t - seg.T0;
                float x = seg.From.X + (seg.To.X - seg.From.X) * u;
                float y = seg.From.Y + (seg.To.Y - seg.From.Y) * u - seg.P0 * 4 * u * (1 - u);
                Stamp(fb, BirdFly[kind], BirdFly[kind].Pages[(int)(s * 24) % 6], x, y, mirror, null, 1f, 254);
                break;
            }
        }
    }

    public static float Dist(PointF a, PointF b) => MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
}

/// <summary>A story's script: one track per actor, each a list of timed segments, and the builders that write them.</summary>
internal sealed class Script
{
    public readonly List<List<Seg>> Tracks = new();
    private readonly Cast _c;

    public Script(Cast cast, int actors)
    {
        _c = cast;
        for (int i = 0; i < actors; i++) Tracks.Add(new List<Seg>());
    }

    public List<Seg> this[int i] => Tracks[i];

    public static Seg Add(List<Seg> track, Kind kind, float t0, float dur, PointF from, PointF to, int dir, bool acorn = false, bool rail = false, float p0 = 0f, float p1 = 0f, bool look = false, bool up = false, bool[]? stencil = null)
    {
        var seg = new Seg { Kind = kind, T0 = t0, T1 = t0 + dur, From = from, To = to, Dir = dir, Acorn = acorn, Rail = rail, P0 = p0, P1 = p1, Look = look, Up = up, Stencil = stencil };
        track.Add(seg);
        return seg;
    }

    public PointF OnGround(float x) => new(x, _c.WalkY(x));
    public PointF OnRail(float x) => new(x, _c.RailAt(x) - 1.5f);

    /// <summary>
    /// A bounding run from "from" to "to" (the feet), paged at the bound the art was painted at (a bound squeezed to fit a
    /// short run slid the feet at twice the body's speed). A run on the ground shorter than a bound and a half is a hop
    /// instead. Returns when it ends.
    /// </summary>
    public float Run(List<Seg> track, float t0, PointF from, PointF to, bool acorn, bool rail)
    {
        float dist = Cast.Dist(from, to);
        float boundPx = Cast.BoundLen * _c.K;
        if (!rail && dist < 1.5f * boundPx) return Hop(track, t0, from, to, to.X >= from.X ? 1 : -1, acorn, 0.3f);
        float dur = dist / (Cast.Speed * _c.S.U);
        Add(track, Kind.Run, t0, dur, from, to, to.X >= from.X ? 1 : -1, acorn, rail, boundPx);
        return t0 + dur;
    }

    public static float Hop(List<Seg> track, float t0, PointF from, PointF to, int dir, bool acorn, float height = 0.55f)
    {
        Add(track, Kind.Hop, t0, Cast.HopFor, from, to, dir, acorn, p0: height);
        return t0 + Cast.HopFor;
    }

    public static float Turn(List<Seg> track, float t0, PointF at, int toDir, bool acorn)
    {
        Add(track, Kind.Turn, t0, Cast.TurnFor, at, at, toDir, acorn);
        return t0 + Cast.TurnFor;
    }

    public static float Rise(List<Seg> track, float t0, PointF at, int dir, bool acorn, int way = 1)
    {
        Add(track, Kind.Rise, t0, Cast.RiseFor, at, at, dir, acorn, p0: way);
        return t0 + Cast.RiseFor;
    }

    public float Climb(List<Seg> track, float t0, PointF from, PointF to, bool up, bool acorn)
    {
        float dur = Cast.Dist(from, to) / (Cast.ClimbSpeed * _c.S.U);
        Add(track, Kind.Climb, t0, dur, from, to, 1, acorn, rail: true, up: up);
        return t0 + dur;
    }

    /// <summary>
    /// Pitching up onto the bark at the foot of a climb. The pages face right; arriving facing left, the squirrel turns
    /// first (sitting up) and sinks back to the crouch before it mounts (the turn's upright sit straight into the
    /// mount's stretched leap was a 25 px jump of the head in one frame).
    /// </summary>
    public static float MountUp(List<Seg> track, float t0, PointF at, int facing, bool acorn)
    {
        if (facing < 0) { t0 = Turn(track, t0, at, 1, acorn); t0 = Rise(track, t0, at, 1, acorn, -1); }
        Add(track, Kind.Mount, t0, Cast.MountFor, at, at, 1, acorn, up: true);
        return t0 + Cast.MountFor;
    }

    /// <summary>Coming off the bark onto the rail after a head-up climb (facing right, crouched); if the next move goes left it rises to a sit, turns, and the hop that follows starts from the sit.</summary>
    public static float Dismount(List<Seg> track, float t0, PointF at, int nextDir, bool acorn)
    {
        Add(track, Kind.Mount, t0, Cast.MountFor, at, at, 1, acorn, up: false);
        t0 += Cast.MountFor;
        if (nextDir < 0) { t0 = Rise(track, t0, at, 1, acorn); t0 = Turn(track, t0, at, -1, acorn); }
        return t0;
    }

    /// <summary>The end of a head-first descent: the body pitches from head-down to level and ends facing LEFT, crouched (a hop straight out of the descent snapped it round 180 degrees); if the next move goes right it rises to a sit and turns.</summary>
    public static float DismountDown(List<Seg> track, float t0, PointF at, int nextDir, bool acorn)
    {
        Add(track, Kind.Mount, t0, Cast.MountFor, at, at, -1, acorn, up: false, p0: 1f);
        t0 += Cast.MountFor;
        if (nextDir > 0) { t0 = Rise(track, t0, at, -1, acorn); t0 = Turn(track, t0, at, 1, acorn); }
        return t0;
    }

    public static float Sit(List<Seg> track, float t0, float dur, PointF at, int dir, bool acorn, bool look = false)
    {
        Add(track, Kind.Sit, t0, dur, at, at, dir, acorn, look: look);
        return t0 + dur;
    }

    public static float Chatter(List<Seg> track, float t0, float dur, PointF at, int dir, bool acorn, int who)
    {
        Add(track, Kind.Chatter, t0, dur, at, at, dir, acorn, p0: who);
        return t0 + dur;
    }

    public static float Dig(List<Seg> track, float t0, float dur, PointF at, int dir, float rate, bool earth, bool acorn = false, bool rail = false)
    {
        Add(track, Kind.Dig, t0, dur, at, at, dir, acorn, rail, p0: rate, p1: earth ? 1f : 0f);
        return t0 + dur;
    }

    /// <summary>A mouse's dash from "from" to "to" at its pace (with a nut if "nut"), returns when it arrives.</summary>
    public float Mouse(List<Seg> track, float t0, PointF from, PointF to, bool nut, bool[]? stencil = null, float speed = 1f)
    {
        float dur = Cast.Dist(from, to) / (Cast.MouseSpeed * speed * _c.S.U);
        Add(track, Kind.Mouse, t0, dur, from, to, to.X >= from.X ? 1 : -1, nut, stencil: stencil);
        return t0 + dur;
    }

    public static float MouseStill(List<Seg> track, float t0, float dur, PointF at, int dir, bool nut, bool[]? stencil = null)
    {
        Add(track, Kind.Mouse, t0, dur, at, at, dir, nut, stencil: stencil);
        return t0 + dur;
    }

    /// <summary>A bird's hops from "from" to "to" at its pace. kind 0 a junco, 1 a sparrow.</summary>
    public float BirdHop(List<Seg> track, float t0, PointF from, PointF to, int kind)
    {
        float dur = Math.Max(0.25f, Cast.Dist(from, to) / (0.12f * _c.S.U));
        Add(track, Kind.Bird, t0, dur, from, to, to.X >= from.X ? 1 : -1, p0: 3f, p1: kind);
        return t0 + dur;
    }

    public static float BirdStill(List<Seg> track, float t0, float dur, PointF at, int dir, int kind, int what)
    {
        Add(track, Kind.Bird, t0, dur, at, at, dir, p0: what, p1: kind);
        return t0 + dur;
    }

    /// <summary>A bird's flight from "from" to "to" along an arc "lift" high (pixels), at its pace.</summary>
    public float BirdFly(List<Seg> track, float t0, PointF from, PointF to, int kind, float lift)
    {
        float dur = Math.Max(0.4f, Cast.Dist(from, to) / (0.45f * _c.S.U) + 0.2f);
        Add(track, Kind.BirdFly, t0, dur, from, to, to.X >= from.X ? 1 : -1, p0: lift, p1: kind);
        return t0 + dur;
    }

    /// <summary>Hops that follow a run start from the push (no crouch); hops that lead into a run end at the landing (no settle). Call once the script is written.</summary>
    public void FixHops()
    {
        foreach (var track in Tracks)
            for (int i = 0; i < track.Count; i++)
            {
                if (track[i].Kind != Kind.Hop) continue;
                bool fromRun = i > 0 && track[i - 1].Kind is Kind.Run or Kind.Mount or Kind.Climb;
                bool toRun = i + 1 < track.Count && track[i + 1].Kind is Kind.Run or Kind.Mount or Kind.Climb;
                track[i].P1 = (fromRun ? 1 : 0) + (toRun ? 2 : 0);
            }
    }

    /// <summary>When the last segment of any track ends.</summary>
    public float End
    {
        get
        {
            float end = 0;
            foreach (var track in Tracks) foreach (Seg s in track) end = Math.Max(end, s.T1);
            return end;
        }
    }

    public void Draw(FrameBuffer fb, float t)
    {
        foreach (var track in Tracks)
            foreach (Seg s in track)
                if (t >= s.T0 && t < s.T1) { _c.DrawSeg(fb, s, t); break; }
    }

    public void Log(StreamWriter sw, string[] names)
    {
        for (int i = 0; i < Tracks.Count; i++)
            foreach (Seg s in Tracks[i])
                sw.WriteLine($"  {names[Math.Min(i, names.Length - 1)],-6} {s.Kind,-9} {s.T0,6:F2} to {s.T1,6:F2}  ({s.From.X:F0},{s.From.Y:F0}) to ({s.To.X:F0},{s.To.Y:F0}) dir {s.Dir} acorn {s.Acorn} rail {s.Rail} p0 {s.P0:F2} p1 {s.P1:F2}");
    }
}

/// <summary>One little scene. Begin rolls its dice and writes its script; Draw draws its props and its script from the story's clock.</summary>
internal abstract class Story
{
    public abstract string Name { get; }
    /// <summary>The props it needs (HappeningDirector claims): it never begins while a happening holds one, and holds them while it plays.</summary>
    public abstract string Claims { get; }
    public virtual bool CanBegin => true;
    /// <summary>Rolls the dice and writes the script; returns how long the story runs.</summary>
    public abstract float Begin(Random rng);
    /// <summary>Once a frame while it plays, with the story's clock, before anything draws (a story that drives a borrowed animal moves it here).</summary>
    public virtual void Update(float t) { }
    public abstract void Draw(FrameBuffer fb, float t);
    /// <summary>Called when the story ends (or is cut): return anything borrowed.</summary>
    public virtual void End() { }
}

/// <summary>Deals the stories, one every minute or two, and draws the one that is on.</summary>
internal sealed class StoryDirector
{
    private readonly ThanksgivingScenery _s;
    private readonly HappeningDirector _director;
    private readonly Random _rng;
    private readonly Cast _cast;
    private readonly Story[] _stories;
    private readonly List<int> _deck = new();
    private readonly string? _only;
    private Story? _on;
    private double _time, _next;
    private float _t, _end;
    private readonly bool _quick;

    public Cast Cast => _cast;
    /// <summary>Asked before a story is dealt: true holds the deal a second (a patch squirrel on a trunk, where the caper's squirrel climbs).</summary>
    public Func<bool>? Hold;
    /// <summary>True while a story is on (the patch's own life keeps out of its way).</summary>
    public bool Playing => _on != null;

    public StoryDirector(ThanksgivingScenery s, HappeningDirector director, Random sceneRng, Noticeboard board, Func<Cast, Story[]> stories)
    {
        _s = s;
        _director = director;
        _rng = new Random(sceneRng.Next());
        _cast = new Cast(s, board);
        _stories = stories(_cast);
        // Test aids: PECKWORKS_STORY=1 starts at once with short rests (PECKWORKS_CAPER=1 too), 0 never; PECKWORKS_STORYPICK names the story to play, every time.
        string? ask = Environment.GetEnvironmentVariable("PECKWORKS_STORY") ?? Environment.GetEnvironmentVariable("PECKWORKS_CAPER");
        _quick = ask == "1";
        _next = ask == "1" ? 4.0 : ask == "0" ? double.MaxValue : 8 + 14 * _rng.NextDouble();    // quick: at 4 s exactly (the cast is built by then), so a frame dump's story time is its process time less four
        _only = Environment.GetEnvironmentVariable("PECKWORKS_STORYPICK");
    }

    public void Update(float dt)
    {
        _time += dt;
        if (_on != null)
        {
            _t += dt;
            if (_t >= _end)
            {
                _on.End();
                _on = null;
                _director.SceneClaims = null;
                _next = _time + (_quick ? 3 : 35 + 50 * _rng.NextDouble());
            }
            else _on.Update(_t);
            return;
        }
        if (!_cast.Ready || _time < _next) return;
        if (Hold?.Invoke() == true) { _next = _time + 1; return; }
        // The deck: every story once before any repeats; a story that cannot begin now (a prop held, not able) waits its turn.
        if (_deck.Count == 0)
        {
            for (int i = 0; i < _stories.Length; i++) _deck.Add(i);
            for (int i = _deck.Count - 1; i > 0; i--) { int j = _rng.Next(i + 1); (_deck[i], _deck[j]) = (_deck[j], _deck[i]); }
        }
        for (int i = _deck.Count - 1; i >= 0; i--)
        {
            Story st = _stories[_deck[i]];
            if (_only != null && !string.Equals(st.Name, _only, StringComparison.OrdinalIgnoreCase)) continue;
            if (!st.CanBegin || _director.Claimed(st.Claims)) continue;
            if (_only == null) _deck.RemoveAt(i);
            _on = st;
            _t = 0;
            _end = st.Begin(_rng);
            _director.SceneClaims = st.Claims;
            string? log = Environment.GetEnvironmentVariable("PECKWORKS_CAPERLOG");
            if (!string.IsNullOrEmpty(log))
            {
                using var sw = new StreamWriter(log, append: true);
                sw.WriteLine($"story {st.Name} at {_time:F1}s, runs {_end:F2}s");
            }
            return;
        }
        _next = _time + 2;                                   // nothing could begin: look again in a moment
    }

    public void Draw(FrameBuffer fb)
    {
        if (_on == null || !_cast.Ready) return;
        _on.Draw(fb, _t);
    }
}
