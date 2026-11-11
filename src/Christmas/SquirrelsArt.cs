using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using Peckworks.Screensavers.Core;
using Peckworks.Screensavers.Core.Animals;

namespace Christmas;

/// <summary>
/// The pictures of Christmas's squirrels (2026-11-10). THE BODY IS NOT DRAWN HERE: it is the shared animal library's
/// squirrel (Core/Animals/Squirrel.cs, moved there out of Thanksgiving's SquirrelArt, where it took many rounds and ended
/// with the owner's approval), in its new WINTER coat (Squirrel.Coat.Winter: rust back, grey flanks, long dark ear
/// tufts, a bushier tail). This file only asks the library for its flip-books at four sizes and adds the few pages
/// that are Christmas's own: a scold on a trunk, stuffing cheeks with seed, a pine cone to carry and to eat, the
/// pale scrape a cache leaves in the snow, and a puff of snow.
///
/// FEYNMAN VERSION: the snowfield runs from the far pines to the bottom edge, and a squirrel at the pines is less
/// than half as big on screen as one at the log. A flip-book cannot be scaled per frame (it would shimmer), so the
/// squirrel is painted at FOUR sizes ("levels") and an animal uses the level nearest its lane; at the rare moment
/// it crosses from one level to the next the two sizes are dissolved over a few frames (Squirrels.cs) so the size
/// never pops.
///
/// WHAT EVERY SQUIRREL IS (the proportions the library already paints, for the record): a head-and-body of about
/// 1.1 squirrel units from nose to rump, 0.2 m in life, so a unit is about 0.18 m; the tail as long as the body;
/// the hind leg folds like a Z (the hock goes forward), the bound lands on the forepaws first and the hind feet
/// swing PAST them; the sit is a question mark with the tail over the back. Size on screen: 0.2 m times a
/// storybook factor 1.9 times the field's pixels to the metre at the animal's feet.
/// </summary>
internal sealed class SquirrelLevel
{
    public float K;                              // pixels to the squirrel unit at this level
    public SquirrelBooks B = null!;              // the library's books in the winter coat
    public SquirrelSheet RunCone, EatCone, Stuff, Scold;
    public Sprite Shadow = null!;                // a faint blue ellipse under the feet
    public int ShadowOx, ShadowOy;
}

internal static class SquirrelsArt
{
    /// <summary>Real size and storybook factor: the field's pixels to the metre times this is a squirrel unit (a unit is 0.18 m of body, and 0.2 m x 1.9 over 1.13 units is 0.336 of a metre).</summary>
    public const float UnitPerMetre = 0.336f;

    public const int StuffPages = 8, ScoldPages = 12;

    public static SquirrelLevel BuildLevel(float k)
    {
        var lv = new SquirrelLevel { K = k };
        var coat = Squirrel.Coat.Winter;
        var b = SquirrelBooks.Build(coat, k);
        lv.B = b;
        int w = b.GroundW, h = b.GroundH, ox = b.GroundOx, by = b.GroundBy;
        SquirrelSheet Ground(int n, Func<int, Squirrel.Pose> pose) => SquirrelBooks.Make(n, w, h, ox, by, i => { var q = pose(i); q.Coat = coat; return Squirrel.Page(q, k, w, h, ox, by); });
        // A pine cone in the mouth on the bound (the library's RunNut is an acorn), and in the paws, being eaten.
        lv.RunCone = Ground(SquirrelBooks.RunPages, i => { var q = Squirrel.Bound(i / (float)SquirrelBooks.RunPages, SquirrelBooks.BoundLen); q.Acorn = 1; q.Item = 1; return q; });
        lv.EatCone = Ground(4, i => { var q = Squirrel.Sit(1f, 0, false, true); q.Acorn = 2; q.Item = 1; q.Nib = i / 3f; return q; });
        // STUFFING CHEEKS: sitting up, a seed turned in both paws, the nose working fast (Nib is the mouth opening), the head
        // bobbing a little with each bite. Eight pages make one chew cycle; the tail stays curled over the back.
        lv.Stuff = Ground(StuffPages, i =>
        {
            float ph = MathF.Tau * i / StuffPages;
            var q = Squirrel.Sit(1f, 0, false, true);
            q.Acorn = 2; q.Item = 2; q.Nib = 0.5f + 0.5f * MathF.Sin(ph);
            q.HeadTilt += 4f * MathF.Sin(ph); q.Head = new PointF(q.Head.X, q.Head.Y - 0.015f * (0.5f + 0.5f * MathF.Sin(ph)));
            q.TailBase += 5f * MathF.Sin(ph * 0.5f);
            return q;
        });
        // THE SCOLD, clinging head-up to a trunk (the climb's own pose, so the seam into it is the same picture): the tail
        // lashes sideways in a smooth ripple and the head nods, a chatter. Anchored exactly as the library's Up sheet.
        int cw = (int)(1.9f * k) + 6, ch = (int)(2.55f * k) + 6, cox = (int)(1.9f * k) + 3, cby = (int)(0.8f * k) + 3;
        lv.Scold = SquirrelBooks.Make(ScoldPages, cw, ch, cox, cby, i =>
        {
            float ph = MathF.Tau * i / ScoldPages;
            var q = SquirrelBooks.Cling(0, -90f, coat);
            q.TailFlick = 34f * MathF.Max(0f, MathF.Sin(ph)) ; q.TailWave = 6f; q.TailWavePhase = ph;
            q.HeadTilt += 5f * MathF.Sin(ph * 2f);
            return Squirrel.Page(q, k, cw, ch, cox, cby);
        });
        int sw = (int)(1.5f * k) + 6, sh = Math.Max(5, (int)(0.30f * k));
        lv.ShadowOx = sw / 2; lv.ShadowOy = sh / 2;
        lv.Shadow = Sprite.Paint(sw, sh, g => { using var br = new SolidBrush(Color.FromArgb(58, 96, 118, 180)); g.FillEllipse(br, 1, 1, sw - 2, sh - 2); });
        return lv;
    }

    /// <summary>
    /// The pale scrape a cache leaves in the snow: a shallow dish a shade BLUER than the snow beside it (a dent in snow
    /// is in its own shadow), a lip of fluffed lighter snow thrown up round it, and a few crumbs. NEVER a dark opening:
    /// the owner's line is "not black holes next to a pond".
    /// </summary>
    public static Sprite Scrape(float k)
    {
        int w = Math.Max(8, (int)(0.9f * k)), h = Math.Max(4, (int)(0.34f * k));
        return Sprite.Paint(w + 4, h + 4, g =>
        {
            using (var rim = new SolidBrush(Color.FromArgb(150, 244, 248, 255))) g.FillEllipse(rim, 0, 0, w + 3, h + 3);
            using (var dish = new SolidBrush(Color.FromArgb(150, 188, 204, 236))) g.FillEllipse(dish, w * 0.12f + 1, h * 0.22f + 1, w * 0.78f, h * 0.62f);
            using (var lit = new SolidBrush(Color.FromArgb(120, 236, 243, 255))) g.FillEllipse(lit, w * 0.2f + 1, h * 0.30f + 1, w * 0.5f, h * 0.22f);
        });
    }

    /// <summary>One soft round speck of snow (for the dug and thrown puffs): radius 2 to 4.</summary>
    public static Sprite Speck(int r) => Sprite.Glow(r, Color.FromArgb(240, 246, 255));

    // ================================================================ the pose sheet
    /// <summary>PECKWORKS_SHEET=squirrels: every page of every flip-book at the biggest level, labelled, on snow and on night blue (review\sheet_squirrels.png).</summary>
    public static void WriteSheet(SquirrelLevel lv, string path)
    {
        var rows = new List<(string Name, SquirrelSheet Sh, int Step)>
        {
            ("run", lv.B.Run, 4), ("run + cone", lv.RunCone, 8), ("sit tail 0", lv.B.Sit[0], 1), ("sit tail 2", lv.B.Sit[2], 1), ("look", lv.B.Look, 1), ("rise", lv.B.Rise, 1),
            ("turn", lv.B.Turn, 1), ("curl", lv.B.Curl, 1), ("dig", lv.B.Dig, 1), ("eat nut", lv.B.Eat, 1), ("eat cone", lv.EatCone, 1), ("stuff (seed)", lv.Stuff, 1),
            ("hop", lv.B.Hop, 2), ("up", lv.B.Up, 4), ("down", lv.B.Down, 4), ("mount up", lv.B.MountUp, 1), ("mount down", lv.B.MountDown, 1), ("scold", lv.Scold, 1),
        };
        int cellW = 0, cellH = 0, perRow = 12, labelH = 14, lines = 0;
        foreach (var r in rows) { cellW = Math.Max(cellW, r.Sh.W); cellH = Math.Max(cellH, r.Sh.H); int n = (r.Sh.Pages.Length + r.Step - 1) / r.Step; lines += (n + perRow - 1) / perRow; }
        int W = cellW * perRow, half = lines * cellH + rows.Count * labelH, H = half * 2;
        var canvas = new int[W * H];
        for (int i = 0; i < canvas.Length; i++) canvas[i] = i < W * half ? unchecked((int)0xFFDEE9FC) : unchecked((int)0xFF14245C);   // snow's colour (222, 233, 252) above, night blue (20, 36, 92) below
        var labels = new List<(string, int, bool)>();
        for (int night = 0; night < 2; night++)
        {
            int y = night * half;
            foreach (var r in rows)
            {
                labels.Add((r.Name, y + 1, night == 1));
                y += labelH;
                int n = 0;
                for (int i = 0; i < r.Sh.Pages.Length; i += r.Step)
                {
                    int cx = (n % perRow) * cellW, cy = y + (n / perRow) * cellH;
                    var spr = r.Sh.Pages[i];
                    for (int py = 0; py < spr.Height; py++)
                        for (int px = 0; px < spr.Width; px++)
                        {
                            uint c = spr.PixelAt(px, py); int a = (int)(c >> 24);
                            if (a == 0) continue;
                            int at = (cy + py) * W + cx + px; uint bg = unchecked((uint)canvas[at]);
                            int rr = (int)((bg >> 16) & 255) + ((((int)((c >> 16) & 255) - (int)((bg >> 16) & 255)) * a) >> 8);
                            int gg = (int)((bg >> 8) & 255) + ((((int)((c >> 8) & 255) - (int)((bg >> 8) & 255)) * a) >> 8);
                            int bb = (int)(bg & 255) + ((((int)(c & 255) - (int)(bg & 255)) * a) >> 8);
                            canvas[at] = unchecked((int)0xFF000000) | (rr << 16) | (gg << 8) | bb;
                        }
                    n++;
                }
                y += ((n + perRow - 1) / perRow) * cellH;
            }
        }
        using var bmp = new Bitmap(W, H, PixelFormat.Format32bppArgb);
        var bits = bmp.LockBits(new Rectangle(0, 0, W, H), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        System.Runtime.InteropServices.Marshal.Copy(canvas, 0, bits.Scan0, canvas.Length);
        bmp.UnlockBits(bits);
        using (var g = Graphics.FromImage(bmp))
            foreach (var (name, ly, nightRow) in labels) g.DrawString(name, SystemFonts.DefaultFont, nightRow ? Brushes.White : Brushes.Black, 2, ly);
        string? dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        bmp.Save(path, ImageFormat.Png);
    }
}