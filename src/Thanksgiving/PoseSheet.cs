using System.Drawing.Imaging;

namespace Thanksgiving;

/// <summary>
/// A test aid for drawing an animal: PECKWORKS_SHEET=squirrel:C:\path\sheet.png paints every pose the squirrel
/// can take, big (120 pixels to the unit), tiles them on one picture, writes it and quits before the saver starts.
/// A painter is judged on a sheet like this first (every pose at once, at 8x) and only then watched moving: a
/// bad tail or a rat's face is found here in one look, not in a frame dump.
/// </summary>
internal static class PoseSheet
{
    public static bool TryWrite()
    {
        string? ask = Environment.GetEnvironmentVariable("PECKWORKS_SHEET");
        if (string.IsNullOrEmpty(ask) || !ask.Contains(':')) return false;
        string who = ask[..ask.IndexOf(':')].ToLowerInvariant(), path = ask[(ask.IndexOf(':') + 1)..];
        const float k = 120f;
        int w = (int)(2.7f * k), h = (int)(2.0f * k), ox = (int)(1.8f * k), by = h - 8;
        (string Name, Action<Graphics> Paint)[] poses = who switch
        {
            "squirrel" => Squirrel(),
            "coyote" => Coyote(),
            "rabbit" => Rabbit(),
            _ => throw new ArgumentException($"PECKWORKS_SHEET='{ask}': the sheet is squirrel:, coyote: or rabbit: followed by the png's path"),
        };
        const int perRow = 4;
        int rows = (poses.Length + perRow - 1) / perRow;
        using var sheet = new Bitmap(w * perRow, h * rows, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(sheet))
        {
            g.Clear(Color.FromArgb(150, 110, 60));                                     // the patch's earth, so the colours are judged against it
            for (int i = 0; i < poses.Length; i++)
            {
                int cx = (i % perRow) * w, cy = (i / perRow) * h;
                var st = g.Save();
                g.SetClip(new Rectangle(cx, cy, w, h));
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.TranslateTransform(cx + ox, cy + by);
                g.ScaleTransform(k, -k);
                poses[i].Paint(g);
                g.Restore(st);
                using var pen = new Pen(Color.FromArgb(90, 0, 0, 0), 1f);
                g.DrawLine(pen, cx, cy + by, cx + w, cy + by);                         // the ground line
                g.DrawRectangle(pen, cx, cy, w - 1, h - 1);
                g.DrawString(poses[i].Name, SystemFonts.DefaultFont, Brushes.White, cx + 4, cy + 4);
            }
        }
        sheet.Save(path, ImageFormat.Png);
        return true;
    }

    private static SquirrelArt.Pose WithNut(SquirrelArt.Pose q) { q.Acorn = q.PawsUp ? 2 : 1; return q; }

    private static (string, Action<Graphics>)[] Squirrel()
    {
        (string Name, SquirrelArt.Pose Pose)[] poses =
        [
            ("bound 0.00", SquirrelArt.Bound(0.00f, 2.2f)), ("bound 0.125", SquirrelArt.Bound(0.125f, 2.2f)), ("bound 0.25", SquirrelArt.Bound(0.25f, 2.2f)), ("bound 0.375", SquirrelArt.Bound(0.375f, 2.2f)),
            ("bound 0.50", SquirrelArt.Bound(0.50f, 2.2f)), ("bound 0.625", SquirrelArt.Bound(0.625f, 2.2f)), ("bound 0.75", SquirrelArt.Bound(0.75f, 2.2f)), ("bound 0.875", SquirrelArt.Bound(0.875f, 2.2f)),
            ("sit", SquirrelArt.Sit(1f, 0, false, false)), ("sit, nut", WithNut(SquirrelArt.Sit(1f, 0, false, true))), ("sit, look", SquirrelArt.Sit(1f, 0, true, false)), ("sit, tail lashed", SquirrelArt.Sit(1f, 2, false, false)),
            ("rise 0.5", SquirrelArt.Sit(0.5f, 0, false, false)), ("dig", SquirrelArt.Dig(0.25f)), ("hop crouch", SquirrelArt.Hop(0.12f)), ("hop flight", SquirrelArt.Hop(0.5f)),
            ("turn 0.25", SquirrelArt.Turn(0.25f)), ("turn 0.5 (front)", SquirrelArt.Turn(0.5f)), ("chatter", SquirrelArt.Chatter(2, 1)), ("dazed", SquirrelArt.Dazed(12f)),
        ];
        return poses.Select(p => (p.Name, (Action<Graphics>)(g => { if (p.Pose.Rotate != 0f) g.RotateTransform(p.Pose.Rotate); SquirrelArt.Paint(g, p.Pose); }))).ToArray();
    }

    private static (string, Action<Graphics>)[] Coyote()
    {
        (string Name, CoyoteArt.Pose Pose)[] poses =
        [
            ("trot 0.0", CoyoteArt.Trot(0f, 1.3f)), ("trot 0.25", CoyoteArt.Trot(0.25f, 1.3f)), ("trot 0.5", CoyoteArt.Trot(0.5f, 1.3f)), ("trot 0.75", CoyoteArt.Trot(0.75f, 1.3f)),
            ("gallop 0.0", CoyoteArt.Gallop(0f, 2.4f)), ("gallop 0.2", CoyoteArt.Gallop(0.2f, 2.4f)), ("gallop 0.45", CoyoteArt.Gallop(0.45f, 2.4f)), ("gallop 0.8", CoyoteArt.Gallop(0.8f, 2.4f)),
            ("stand", CoyoteArt.Stand()), ("sit", CoyoteArt.Sit()), ("dig", CoyoteArt.Dig(0.25f)), ("carry", CoyoteArt.Carry()),
            ("shake", CoyoteArt.Shake(25f)), ("pounce 0.5", CoyoteArt.Pounce(0.5f)), ("pounce 0.8", CoyoteArt.Pounce(0.8f)), ("pull", CoyoteArt.Pull()),
        ];
        return poses.Select(p => (p.Name, (Action<Graphics>)(g => CoyoteArt.Paint(g, p.Pose)))).ToArray();
    }

    private static (string, Action<Graphics>)[] Rabbit()
    {
        (string Name, RabbitArt.Pose Pose)[] poses =
        [
            ("sit", RabbitArt.Sit()), ("graze", RabbitArt.Graze()), ("hop 0.1", RabbitArt.Hop(0.1f)), ("hop 0.3", RabbitArt.Hop(0.3f)),
            ("hop 0.5", RabbitArt.Hop(0.5f)), ("hop 0.7", RabbitArt.Hop(0.7f)), ("hop 0.9", RabbitArt.Hop(0.9f)), ("flee 0.4", RabbitArt.Hop(0.4f, true)),
            ("dead", RabbitArt.Dead()), ("limp (from a mouth)", RabbitArt.Limp()),
        ];
        return poses.Select(p => (p.Name, (Action<Graphics>)(g => { if (p.Name.StartsWith("limp")) g.TranslateTransform(0f, 1.1f); RabbitArt.Paint(g, p.Pose); }))).ToArray();
    }
}
