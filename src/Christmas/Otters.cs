using System.Drawing.Imaging;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Peckworks.Screensavers.Core;
using Peckworks.Screensavers.Core.Minds;

namespace Christmas;

/// <summary>Every flip-book the otters use, painted once. [page, yaw bin] unless a set is side-on only.</summary>
internal sealed class OtterPages
{
    public Sprite[,] Gallop = null!, Start = null!, Throw = null!, GetUp = null!, Glide = null!, Rest = null!, Up = null!, Stand = null!;
    public Sprite[] Flop = null!, RollCycle = null!, SitDown = null!, Dive = null!;
    public Sprite[,] SitEat = null!;             // [fish left 0..2, chew 0..7]
    public Sprite[,,] Surf = null!;              // [rise 0..5, yaw bin, fish 0/1]
    public Sprite[,] Haul = null!;               // [page 0..7, fish 0/1]
    public Sprite[] Shadow = null!;              // by yaw bin
    public Sprite[] EatEnd = null!, Ball = null!, PairIn = null!;
    public Sprite[,,] RestVar = null!;           // [variant 0 blink, 1 ear flick, 2 whiskers, 3 tail curl; head 1..3 (index 0..2); yaw bin]
    public Sprite[,] Scratch = null!;            // [page, yaw bin]
    public Sprite Droplet = null!;
    public Sprite[] Puff = null!;                // three sizes
    public int W, H, OX, OY;                     // the standard sheet
    public int WideW, WideOX, DiveW, DiveOX, SurfW, SurfH, SurfOX, SurfOY;
    public float P;                              // pixels in a metre

    // SILKY: a transition of T seconds has at least 30 * T pages (a page is held two frames at most while the body moves).
    public const int GallopPages = 32, StartPages = 14, ThrowPages = 11, GetUpPages = 17, StandPages = 12, FlopPages = 18, RollPages = 36, SitDownPages = 17, DivePages = 40, HaulPages = 52, SurfPages = 14;
    public const int GlidePages = 32, RestHeads = 17, EatChew = 24, EatEndPages = 17, ScratchPages = 24, PairPages = 10, BallPages = 36;
}

/// <summary>
/// STUB-REPLACING RESIDENT (2026-11-10): the river otters that live in the frozen pond. See the long comment in
/// OttersArt.cs for the animal and how it is drawn; the mind is below.
/// </summary>
internal sealed partial class Otters : Resident
{
    private volatile OtterPages? _pg;
    private readonly int _count;

    public Otters(ChristmasScenery s, int count, Random sceneRng, Noticeboard board) : base(s, sceneRng, board)
    {
        // Test variable: PECKWORKS_OTTERCOUNT overrides the knob (0 is the 'without otters' timing run).
        if (int.TryParse(Environment.GetEnvironmentVariable("PECKWORKS_OTTERCOUNT"), out int forced)) count = forced;
        _count = Math.Clamp(count, 0, 6);
        float p = Math.Max(4f, S.Metre(S.Pond.At.Y));
        if (_count > 0) BuildInBackground(() =>
        {
            try
            {
                long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
                OtterPages built = Build(p);
                long t1 = System.Diagnostics.Stopwatch.GetTimestamp();
                Warm(built); _pg = built;
                if (Environment.GetEnvironmentVariable("PECKWORKS_MINDLOG") is { Length: > 0 } && MindLog.Open() is { } lg)
                    lock (lg) lg.WriteLine($"0.00 Otter build {System.Diagnostics.Stopwatch.GetElapsedTime(t0, t1).TotalMilliseconds:0} ms, warm-up {System.Diagnostics.Stopwatch.GetElapsedTime(t1).TotalMilliseconds:0} ms");
            }
            catch (Exception e) { if (Environment.GetEnvironmentVariable("PECKWORKS_SHEET") is { } sh && sh.Contains(';')) File.WriteAllText(sh[(sh.IndexOf(';') + 1)..] + ".err.txt", e.ToString()); throw; }
        });
        else BuildInBackground(() => { });
        Init();
        InitMind();
    }

    private static OtterPages Build(float p)
    {
        var g = new OtterPages { P = p };
        g.W = (int)(1.7f * p); g.H = (int)(0.85f * p); g.OX = g.W / 2; g.OY = (int)(0.66f * p);
        g.WideW = (int)(2.6f * p); g.WideOX = (int)(1.8f * p);
        g.DiveW = (int)(3.1f * p); g.DiveOX = (int)(0.75f * p);
        g.SurfW = (int)(1.0f * p); g.SurfH = (int)(0.75f * p); g.SurfOX = g.SurfW / 2; g.SurfOY = (int)(0.5f * p);
        Sprite Pg(Skel k, int bin) => OttersArt.Page(k, OttersArt.YawDeg(bin), p, g.W, g.H, g.OX, g.OY);
        Sprite[,] Set(int n, Func<int, Skel> pose) { var a = new Sprite[n, OttersArt.Bins]; for (int i = 0; i < n; i++) { Skel k = pose(i); for (int b = 0; b < OttersArt.Bins; b++) a[i, b] = Pg(k, b); } return a; }
        Sprite[] Side(int n, Func<int, Skel> pose) { var a = new Sprite[n]; for (int i = 0; i < n; i++) a[i] = Pg(pose(i), OttersArt.SideBin); return a; }

        g.Gallop = Set(OtterPages.GallopPages, i => OttersArt.Gallop(i / (float)OtterPages.GallopPages));
        Skel rest = OttersArt.Rest(), gath = OttersArt.Gallop(0f);
        g.Start = Set(OtterPages.StartPages, i => Skel.Lerp(rest, gath, Sm(i / (OtterPages.StartPages - 1f))));
        Skel ext = OttersArt.Gallop(9f / 16f), glide0 = OttersArt.Glide(0f);
        g.Throw = Set(OtterPages.ThrowPages, i => Skel.Lerp(ext, glide0, Sm(i / (OtterPages.ThrowPages - 1f))));
        g.GetUp = Set(OtterPages.GetUpPages, i => Skel.Lerp(glide0, rest, Sm(i / (OtterPages.GetUpPages - 1f))));
        g.Glide = Set(OtterPages.GlidePages, i => OttersArt.Glide(MathF.Sin(MathF.Tau * i / OtterPages.GlidePages)));
        g.Rest = Set(OtterPages.RestHeads, i => OttersArt.Rest((i - 8) * 4.75f, 4f));   // 17 head turns, 4.75 degrees apart: the head glides between them (pages 4, 8 and 12 are the old 19 degree heads)
        g.Up = Set(3, i => OttersArt.Upright(i * 0.5f));
        g.RestVar = new Sprite[4, 3, OttersArt.Bins];
        for (int v = 0; v < 4; v++)
            for (int h = 0; h < 3; h++)
            {
                Skel k = OttersArt.Rest(h * 19f - 19f, 4f, 0.15f, v == 0 ? 1f : 0f, v == 1 ? 1f : 0f, v == 2 ? 1f : 0f, v == 3 ? 1f : 0f);
                for (int b = 0; b < OttersArt.Bins; b++) g.RestVar[v, h, b] = Pg(k, b);
            }
        // The hind-foot scratch as a smooth sweep of 24 pages: the keys are the old four poses, visited in the old order
        // (0, 1, 2, 1, 2, 1, 2, 3) and the pose is read off a smooth curve through them.
        {
            float[] ka = [0.4f, 1f, 1f, 1f, 1f, 1f, 1f, 0.45f], ks = [0f, -1f, 1f, -1f, 1f, -1f, 1f, 0f], kh = [0.3f, 1f, 1f, 1f, 1f, 1f, 1f, 0.3f];
            g.Scratch = Set(OtterPages.ScratchPages, i =>
            {
                float u = i / (OtterPages.ScratchPages - 1f) * 7f; int a = Math.Min(6, (int)u); float f = Sm(u - a);
                return OttersArt.Scratch(ka[a] + (ka[a + 1] - ka[a]) * f, ks[a] + (ks[a + 1] - ks[a]) * f, kh[a] + (kh[a + 1] - kh[a]) * f);
            });
        }
        Skel up0 = OttersArt.Upright(0f);
        g.Stand = Set(OtterPages.StandPages, i => Skel.Lerp(rest, up0, Sm(i / (OtterPages.StandPages - 1f))));
        g.Flop = Side(OtterPages.FlopPages, i => { float e = Sm(i / (OtterPages.FlopPages - 1f)); return OttersArt.Roll(180f * e, 0.15f - 0.075f * e, 0f); });
        g.RollCycle = Side(OtterPages.RollPages, i => { float t = i / (float)OtterPages.RollPages; return OttersArt.Roll(180f + 38f * MathF.Sin(MathF.Tau * t), 0.075f, 1f * MathF.Sin(MathF.Tau * t), MathF.Sin(MathF.Tau * t + 1f)); });
        // Eating: from standing with the fish in its mouth, sit up and hold it; then bites (the fish gets shorter).
        Skel restFish = OttersArt.Rest(); restFish = HoldFish(restFish);
        Skel sit0 = OttersArt.SitEat(0f, 1f);
        g.SitDown = Side(OtterPages.SitDownPages, i => Skel.Lerp(restFish, sit0, Sm(i / (OtterPages.SitDownPages - 1f))));
        g.SitEat = new Sprite[3, OtterPages.EatChew];
        float[] fishLeft = [1f, 0.62f, 0.3f];
        for (int f = 0; f < 3; f++)
            for (int c = 0; c < OtterPages.EatChew; c++)
                g.SitEat[f, c] = Pg(OttersArt.SitEat(0.5f + 0.5f * MathF.Sin(MathF.Tau * c / OtterPages.EatChew), fishLeft[f]), OttersArt.SideBin);
        // Head up out of the hole: six heights, nine yaws, with and without a fish.
        g.Surf = new Sprite[OtterPages.SurfPages, OttersArt.Bins, 2];
        for (int r = 0; r < OtterPages.SurfPages; r++)
            for (int b = 0; b < OttersArt.Bins; b++)
                for (int f = 0; f < 2; f++)
                    g.Surf[r, b, f] = OttersArt.Page(OttersArt.Surf(r / (OtterPages.SurfPages - 1f), f * 0.9f), OttersArt.YawDeg(b), p, g.SurfW, g.SurfH, g.SurfOX, g.SurfOY);
        g.Haul = new Sprite[OtterPages.HaulPages, 2];
        for (int i = 0; i < OtterPages.HaulPages; i++)
            for (int f = 0; f < 2; f++)
                g.Haul[i, f] = OttersArt.Page(i < 8 ? OttersArt.Haul(0f, f * 0.9f, 0.26f * (1f - i / 8f)) : OttersArt.Haul((i - 8) / (OtterPages.HaulPages - 9f), f * 0.9f), 0f, p, g.WideW, g.H, g.WideOX, g.OY);
        g.Dive = new Sprite[OtterPages.DivePages];
        for (int i = 0; i < OtterPages.DivePages; i++)
            g.Dive[i] = OttersArt.Page(OttersArt.Dive(i / (OtterPages.DivePages - 1f)), 0f, p, g.DiveW, g.H, g.DiveOX, g.OY);
        Skel upEat = OttersArt.Upright(0.5f, 62f);
        g.EatEnd = Side(OtterPages.EatEndPages, i => Skel.Lerp(upEat, rest, Sm(i / (OtterPages.EatEndPages - 1f))));
        g.Ball = new Sprite[OtterPages.BallPages];
        for (int i = 0; i < OtterPages.BallPages; i++)
        {
            float th = 360f * i / OtterPages.BallPages, wig = MathF.Sin(MathF.Tau * i / (OtterPages.BallPages / 2f));
            g.Ball[i] = OttersArt.Page([OttersArt.Curl(th + 180f, 0.115f, -0.03f, -wig), OttersArt.Curl(th, 0.185f, 0.04f, wig)], 0f, p, g.W, g.H, g.OX, g.OY);
        }
        g.PairIn = new Sprite[OtterPages.PairPages];
        for (int i = 0; i < OtterPages.PairPages; i++)
        {
            float kk = Sm(i / (float)OtterPages.PairPages);
            g.PairIn[i] = OttersArt.Page([Skel.Lerp(OttersArt.Place(rest, -0.38f, false), OttersArt.Curl(0f, 0.185f, 0.04f, 0f), kk), Skel.Lerp(OttersArt.Place(rest, 0.38f, true), OttersArt.Curl(180f, 0.115f, -0.03f, 0f), kk)], 0f, p, g.W, g.H, g.OX, g.OY);
        }
        g.Droplet = Sprite.Paint(4, 4, gr => { using var b = new SolidBrush(Color.FromArgb(235, 214, 230, 255)); gr.FillEllipse(b, 0.5f, 0.5f, 3f, 3f); });
        g.Puff = [Sprite.Glow(Math.Max(3, (int)(0.07f * p)), Color.FromArgb(236, 244, 255)), Sprite.Glow(Math.Max(4, (int)(0.11f * p)), Color.FromArgb(236, 244, 255)), Sprite.Glow(Math.Max(5, (int)(0.16f * p)), Color.FromArgb(236, 244, 255))];
        g.Shadow = new Sprite[OttersArt.Bins];
        for (int b = 0; b < OttersArt.Bins; b++) g.Shadow[b] = OttersArt.Shadow(OttersArt.YawDeg(b), p);
        return g;
    }

    /// <summary>
    /// FIRST-USE COST: the saver runs with tiered JIT off, so every method is compiled in full the first time it is called, and
    /// the first Update of a resident that has never run used to stall for 5 to 14 ms. This runs on the build thread (it only
    /// compiles and draws a dummy frame; it rolls no dice and reads nothing that changes): every method of the otters' own
    /// classes, and of the engine pieces they call every frame, is compiled now, and the stamp, mirror, depth and line paths
    /// are exercised once on a small throwaway frame.
    /// </summary>
    private static void Warm(OtterPages g)
    {
        try
        {
            var fb = new FrameBuffer(96, 64);
            g.Droplet.Draw(fb, 10, 10, 0.8f);
            g.Puff[0].DrawCentered(fb, 30, 30, 0.5f);
            g.Shadow[4].DrawCentered(fb, 40, 40, 1f);
            g.Rest[2, OttersArt.SideBin].Draw(fb, 5, 5, 1f, null, true, new byte[96 * 64], 100);
            fb.Line(new PointF(1, 1), new PointF(50, 40), Color.White, 0.3f, 1.2f);
        }
        catch { }
        var kinds = new List<Type> { typeof(Sprite), typeof(FrameBuffer), typeof(Pace), typeof(Turn), typeof(Organic), typeof(Dice), typeof(Attention), typeof(Noticeboard), typeof(Cooldown), typeof(Chain) };
        void Add(Type t) { kinds.Add(t); foreach (Type n in t.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic)) Add(n); }
        Add(typeof(Otters)); Add(typeof(OttersArt)); Add(typeof(Skel)); Add(typeof(OtterPages));
        const BindingFlags all = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        foreach (Type t in kinds)
        {
            if (t.ContainsGenericParameters) continue;
            foreach (MethodBase m in t.GetMethods(all))
            {
                if (m.IsAbstract || m.ContainsGenericParameters) continue;
                try { RuntimeHelpers.PrepareMethod(m.MethodHandle); } catch { }
            }
            foreach (MethodBase m in t.GetConstructors(all))
                try { RuntimeHelpers.PrepareMethod(m.MethodHandle); } catch { }
        }
    }

    private static float Sm(float t) { t = Math.Clamp(t, 0f, 1f); return t * t * (3f - 2f * t); }

    private static Skel HoldFish(Skel k)
    {
        // A fish across the mouth of a standing otter (the page the haul-out ends on, and the sit-down starts from).
        Skel q = OttersArt.Haul(1f, 0.9f);
        return q;
    }

    /// <summary>Writes every page of every flip-book to one PNG: the snow's colour on top, night blue below (PECKWORKS_SHEET=otters;file.png).</summary>
    private void WriteSheet(OtterPages g, string path)
    {
        var rows = new List<(string Name, List<Sprite> Pages)>();
        List<Sprite> Row(Func<int, Sprite> f, int n) { var l = new List<Sprite>(); for (int i = 0; i < n; i++) l.Add(f(i)); return l; }
        rows.Add(("gallop", Row(i => g.Gallop[i, OttersArt.SideBin], 16)));
        rows.Add(("gallop yaws", Row(i => g.Gallop[3, i], 9)));
        rows.Add(("gallop yaws -90", Row(i => g.Gallop[11, i], 9)));
        rows.Add(("start", Row(i => g.Start[i, OttersArt.SideBin], 5)));
        rows.Add(("throw", Row(i => g.Throw[i, OttersArt.SideBin], 6)));
        rows.Add(("glide", Row(i => g.Glide[i, OttersArt.SideBin], 4)));
        rows.Add(("glide yaws", Row(i => g.Glide[1, i], 9)));
        rows.Add(("getup", Row(i => g.GetUp[i, OttersArt.SideBin], 6)));
        rows.Add(("rest heads", Row(i => g.Rest[i, OttersArt.SideBin], 5)));
        rows.Add(("rest yaws", Row(i => g.Rest[2, i], 9)));
        rows.Add(("eatend", Row(i => g.EatEnd[i], 5)));
        rows.Add(("pair in", Row(i => g.PairIn[i], 3)));
        rows.Add(("scratch", Row(i => g.Scratch[i, OttersArt.SideBin], 4)));
        rows.Add(("rest variants", Row(i => g.RestVar[i, 1, OttersArt.SideBin], 4)));
        rows.Add(("ball", Row(i => g.Ball[i], 12)));
        rows.Add(("stand", Row(i => g.Stand[i, OttersArt.SideBin], 5)));
        rows.Add(("upright", Row(i => g.Up[i, OttersArt.SideBin], 3)));
        rows.Add(("upright yaws", Row(i => g.Up[1, i], 9)));
        rows.Add(("flop", Row(i => g.Flop[i], 6)));
        rows.Add(("roll", Row(i => g.RollCycle[i], 12)));
        rows.Add(("sitdown", Row(i => g.SitDown[i], 5)));
        rows.Add(("sit eat", Row(i => g.SitEat[i / 3 % 3, (i % 3) * 3], 9)));
        rows.Add(("surf", Row(i => g.Surf[i, 4, 0], 6)));
        rows.Add(("surf fish", Row(i => g.Surf[5, i, 1], 9)));
        rows.Add(("haul", Row(i => g.Haul[i, 0], 16)));
        rows.Add(("haul fish", Row(i => g.Haul[i, 1], 16)));
        rows.Add(("dive", Row(i => g.Dive[i], 10)));
        int cell = g.WideW, cellH = g.H, perRow = Math.Max(1, 1900 / cell);
        int total = 0; foreach (var r in rows) total += (r.Pages.Count + perRow - 1) / perRow;
        using var bmp = new Bitmap(perRow * cell, total * (cellH + 14) * 2 + 4);
        using (var gr = Graphics.FromImage(bmp))
        {
            int yy = 0;
            foreach (Color bg in new[] { Color.FromArgb(222, 233, 252), Color.FromArgb(20, 36, 92) })
            {
                gr.FillRectangle(new SolidBrush(bg), 0, yy, bmp.Width, total * (cellH + 14));
                int y = yy;
                foreach (var (name, pages) in rows)
                {
                    gr.DrawString(name, SystemFonts.DefaultFont, bg.R > 100 ? Brushes.Black : Brushes.White, 2, y);
                    y += 14;
                    for (int i = 0; i < pages.Count; i++)
                    {
                        Sprite s = pages[i];
                        using Bitmap sb = ToBitmap(s);
                        int cx = (i % perRow) * cell, cy = y + (i / perRow) * cellH;
                        gr.DrawImage(sb, cx, cy);
                        gr.DrawRectangle(Pens.Gray, cx, cy, cell - 1, cellH - 1);
                    }
                    int lines = (pages.Count + perRow - 1) / perRow;
                    y += lines * cellH;
                    y -= 0;
                }
                yy += total * (cellH + 14);
            }
        }
        bmp.Save(path, ImageFormat.Png);
    }

    private static Bitmap ToBitmap(Sprite s)
    {
        var b = new Bitmap(s.Width, s.Height, PixelFormat.Format32bppArgb);
        var d = b.LockBits(new Rectangle(0, 0, s.Width, s.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        var raw = new int[s.Width * s.Height];
        ReadOnlySpan<uint> src = s.Argb;
        for (int i = 0; i < raw.Length; i++) raw[i] = (int)src[i];
        Marshal.Copy(raw, 0, d.Scan0, raw.Length);
        b.UnlockBits(d);
        return b;
    }

    // ---- the mind lives in OttersMind.cs-less form below (filled in next) ----
    private void Init()
    {
        string? sheet = Environment.GetEnvironmentVariable("PECKWORKS_SHEET");
        if (sheet != null && sheet.StartsWith("otters", StringComparison.OrdinalIgnoreCase) && _count > 0)
        {
            // "otters;file.png", or just "otters" (look.ps1 splits its -Vars on ';'): the sheet then lands in review\sheet_otters.png under the working folder.
            string file = sheet.Length > 7 ? sheet.Substring(7) : Path.Combine("review", "sheet_otters.png");
            // Synchronous (the snapshot run would end before a helper thread finished): test-only path.
            try { WriteSheet(Build(Math.Max(4f, S.Metre(S.Pond.At.Y))), file); } catch (Exception e) { File.WriteAllText(file + ".err.txt", e.ToString()); }
        }
    }
}

