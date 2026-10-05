using System.Diagnostics;
using System.Drawing.Imaging;

namespace Peckworks.Screensavers.Core;

/// <summary>
/// The front door. A screensaver's Main method calls ScreensaverApp.Run and
/// this decides, from the command line, which of the modes to start.
/// </summary>
public static class ScreensaverApp
{
    public static void Run(string[] args, ScreensaverDefinition definition)
    {
        CommandLine cmd = CommandLine.Parse(args);

        switch (cmd.Mode)
        {
            case LaunchMode.Screensaver:
                RunFullScreen(definition);
                break;

            case LaunchMode.Preview:
                // No valid window number means there is nowhere to draw. Just quit.
                if (cmd.WindowHandle == IntPtr.Zero) return;
                Application.Run(ScreensaverWindow.Preview(definition, cmd.WindowHandle));
                break;

            case LaunchMode.Configure:
                RunSettings(definition, cmd.WindowHandle);
                break;

            case LaunchMode.Window:
                Application.Run(ScreensaverWindow.Windowed(definition, new Size(1280, 720)));
                break;

            case LaunchMode.Snapshot:
                RunSnapshot(definition, cmd.Extra);
                break;
        }
    }

    /// <summary>
    /// One full-screen window per monitor. With two monitors you get two
    /// windows, each running its own copy of the scene.
    /// </summary>
    private static void RunFullScreen(ScreensaverDefinition definition)
    {
        foreach (Screen screen in Screen.AllScreens)
            ScreensaverWindow.FullScreen(definition, screen.Bounds).Show();

        // Application.Run() with no form starts the "message loop": the
        // program's heartbeat that delivers mouse moves, key presses, timer ticks
        // and paint requests to our windows. It keeps running until something
        // calls Application.Exit (which ScreensaverWindow does on wake-up).
        Application.Run();
    }

    private static void RunSettings(ScreensaverDefinition definition, IntPtr ownerHandle)
    {
        using Form dialog = definition.CreateSettingsForm();
        if (ownerHandle != IntPtr.Zero)
        {
            // Showing the dialog "owned by" the Screen Saver Settings window
            // keeps it on top of that window, like a normal pop-up.
            dialog.StartPosition = FormStartPosition.CenterParent;
            dialog.ShowDialog(new Win32Owner(ownerHandle));
        }
        else
        {
            dialog.StartPosition = FormStartPosition.CenterScreen;
            dialog.ShowDialog();
        }
    }

    /// <summary>Wraps a raw window number so WinForms will accept it as a dialog owner.</summary>
    private sealed class Win32Owner(IntPtr handle) : IWin32Window
    {
        public IntPtr Handle { get; } = handle;
    }

    /// <summary>
    /// Development helper. Renders the scene off-screen, with no window at all,
    /// and saves a PNG so you (or an automated check) can look at a frame.
    ///
    /// Usage:  MatrixRain.exe /snapshot out.png [width height seconds]
    ///
    /// It also writes out.png.txt with how many milliseconds a frame took, which
    /// is the honest measure of whether the animation will run smoothly. Three
    /// numbers: the average over the whole run; the WORST SECOND (the slowest
    /// stretch of 60 frames in a row); and the SLOWEST single FRAME. The last
    /// two leave out the first second, while the program warms up. Each one
    /// catches what the one before hides: a firework that is slow for half a
    /// second vanishes in a 15 second average and shows in the worst second;
    /// one frozen frame (a stutter) vanishes in both and shows in the slowest
    /// frame.
    /// </summary>
    private static void RunSnapshot(ScreensaverDefinition definition, string[] extra)
    {
        string path = extra.Length > 0 ? extra[0] : "snapshot.png";
        int width = extra.Length > 1 ? int.Parse(extra[1]) : 1920;
        int height = extra.Length > 2 ? int.Parse(extra[2]) : 1080;
        double seconds = extra.Length > 3 ? double.Parse(extra[3], System.Globalization.CultureInfo.InvariantCulture) : 8;

        using IScreensaverScene scene = definition.CreateScene(width, height, isPreview: false);
        var frame = new FrameBuffer(width, height);

        const double step = 1.0 / 60;
        var timer = new Stopwatch();
        int frames = 0;
        var lastSixty = new Queue<double>();   // the last 60 frame times, oldest first: a one-second window that slides along
        double windowSum = 0, worstSecond = 0, slowestFrame = 0, before = 0;
        for (double t = 0; t < seconds; t += step)
        {
            timer.Start();
            scene.Update(step);
            scene.Render(frame);   // render every step, so the timing is realistic
            timer.Stop();
            frames++;

            double now = timer.Elapsed.TotalMilliseconds, took = now - before;
            before = now;
            if (frames <= 60) continue;                         // the first second is warm-up, not the scene
            slowestFrame = Math.Max(slowestFrame, took);
            lastSixty.Enqueue(took);
            windowSum += took;
            if (lastSixty.Count > 60) windowSum -= lastSixty.Dequeue();
            if (lastSixty.Count == 60) worstSecond = Math.Max(worstSecond, windowSum / 60);
        }

        using Bitmap bmp = frame.ToBitmap();
        bmp.Save(path, ImageFormat.Png);
        File.WriteAllText(path + ".txt",
            $"{width}x{height}, {frames} frames, average {timer.Elapsed.TotalMilliseconds / frames:F2} ms per frame (update + render)"
            + (worstSecond > 0 ? $", worst second {worstSecond:F2} ms per frame, slowest frame {slowestFrame:F1} ms" : "") + "\n");
    }
}
