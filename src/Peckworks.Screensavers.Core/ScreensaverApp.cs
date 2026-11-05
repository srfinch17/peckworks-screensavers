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
        if (cmd.Mode != LaunchMode.Preview && cmd.Mode != LaunchMode.Configure) CompileEverythingNow();

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
    /// Compiles every method of the saver and the engine on a helper thread
    /// at start-up, so none is compiled in the middle of a frame.
    ///
    /// FEYNMAN VERSION: a .NET program ships as a recipe, not as machine code;
    /// each method is turned into machine code (the JIT, "just in time") the
    /// first time it is called. Tiered compilation is off for the savers (see
    /// src/Directory.Build.props), so that first call does the full, slow,
    /// optimising compile: ten milliseconds and more for a big method. In a
    /// screensaver most methods are first called on the frame an animal first
    /// appears (the snake's first visit, a lizard running in, the first duck
    /// family), and that frame then ran 12 to 14 ms long: one dropped frame,
    /// right as something new came on (measured 2026-10-20: the "update: snake",
    /// "update: lizards, mice" and "update: frogs, tadpoles" sections each with
    /// a worst frame of 11 to 13 ms against averages under 0.1). So the whole
    /// programme is compiled here, on a thread pool thread, while the window
    /// is being made and the scene pre-warmed. Generic methods are skipped
    /// (they are compiled per type argument and cannot be prepared in the
    /// abstract); a method that will not compile is skipped too.
    /// </summary>
    private static void CompileEverythingNow()
    {
        System.Threading.Tasks.Task.Run(static () =>
        {
            const System.Reflection.BindingFlags all = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static
                | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly;
            var assemblies = new List<System.Reflection.Assembly> { typeof(ScreensaverApp).Assembly };
            if (System.Reflection.Assembly.GetEntryAssembly() is { } entry && !assemblies.Contains(entry)) assemblies.Add(entry);
            // Every type of ours, plus every CLOSED generic type of ours that a field names (Mind<Koi.Doing>, say): a
            // generic type's methods are compiled once per type argument, and only the closed form can be prepared.
            var types = new List<Type>();
            foreach (System.Reflection.Assembly asm in assemblies)
            {
                try { types.AddRange(asm.GetTypes()); }
                catch (System.Reflection.ReflectionTypeLoadException e) { types.AddRange(e.Types.Where(t => t != null)!); }
            }
            var closed = new HashSet<Type>();
            foreach (Type t in types.ToArray())
            {
                System.Reflection.FieldInfo[] fields;
                try { fields = t.GetFields(all); } catch (Exception) { continue; }
                foreach (System.Reflection.FieldInfo f in fields)
                {
                    Type ft = f.FieldType;
                    if (ft.IsConstructedGenericType && assemblies.Contains(ft.GetGenericTypeDefinition().Assembly) && closed.Add(ft)) types.Add(ft);
                }
            }
            foreach (Type t in types)
            {
                if (t.ContainsGenericParameters) continue;
                try
                {
                    foreach (System.Reflection.MethodBase m in t.GetMethods(all).Cast<System.Reflection.MethodBase>().Concat(t.GetConstructors(all)))
                    {
                        if (m.IsAbstract || m.ContainsGenericParameters) continue;
                        try { System.Runtime.CompilerServices.RuntimeHelpers.PrepareMethod(m.MethodHandle); }
                        catch (Exception) { /* a method the runtime will not compile ahead (a P/Invoke stub, say): it compiles on its first call as before */ }
                    }
                }
                catch (Exception) { /* a type whose methods cannot be listed: skipped, the rest still get compiled (one throw used to end the whole pass) */ }
            }
        });
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
    /// frame. Last, how many times the garbage collector ran (gen 0, 1, 2):
    /// a scene that makes garbage every frame pays for it in exactly those
    /// slowest frames, and a gen 2 collection can freeze a frame outright.
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
        int gc0 = GC.CollectionCount(0), gc1 = GC.CollectionCount(1), gc2 = GC.CollectionCount(2);
        long alloc0 = GC.GetTotalAllocatedBytes();
        var timer = new Stopwatch();
        var updateTimer = new Stopwatch();     // Update alone: a slow frame is either the world or the painting, and the number says which
        double worstUpdate = 0;
        int frames = 0;
        var lastSixty = new Queue<double>();   // the last 60 frame times, oldest first: a one-second window that slides along
        double windowSum = 0, worstSecond = 0, slowestFrame = 0, before = 0;
        // PECKWORKS_FRAMETIMES names a file: every frame's cost after the warm-up second, one number
        // per line, in milliseconds. An average and a worst frame cannot show a pattern (every other
        // frame dear, a spike every N frames); a list of all of them can.
        string? frameTimes = Environment.GetEnvironmentVariable("PECKWORKS_FRAMETIMES");
        using var frameTimesOut = string.IsNullOrEmpty(frameTimes) ? null : new StreamWriter(frameTimes);
        for (double t = 0; t < seconds; t += step)
        {
            timer.Start();
            updateTimer.Restart();
            scene.Update(step);
            updateTimer.Stop();
            scene.Render(frame);   // render every step, so the timing is realistic
            timer.Stop();
            if (frames > 60) worstUpdate = Math.Max(worstUpdate, updateTimer.Elapsed.TotalMilliseconds);
            frames++;

            double now = timer.Elapsed.TotalMilliseconds, took = now - before;
            before = now;
            SaveFrameIfAsked(frame, t);
            if (frames <= 60) continue;                         // the first second is warm-up, not the scene
            frameTimesOut?.WriteLine(took.ToString("F3", System.Globalization.CultureInfo.InvariantCulture));
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
            + (worstSecond > 0 ? $", worst second {worstSecond:F2} ms per frame, slowest frame {slowestFrame:F1} ms" : "")
            + $", garbage collections {GC.CollectionCount(0) - gc0}/{GC.CollectionCount(1) - gc1}/{GC.CollectionCount(2) - gc2} (gen 0/1/2)"
            + $", allocated {(GC.GetTotalAllocatedBytes() - alloc0) / 1048576.0 / Math.Max(1, seconds):F1} MB per second, slowest update {worstUpdate:F1} ms\n");
    }

    /// <summary>
    /// A testing aid for motion. A single picture cannot show whether
    /// something MOVES smoothly, so with PECKWORKS_FRAMES set to
    /// "folder;from;count" the snapshot also saves every frame from "from"
    /// seconds on, "count" of them, as numbered PNGs: a flip-book to compare
    /// frame by frame (or to measure how much changes between frames). An
    /// optional fourth part, "folder;from;count;step", keeps only every
    /// step-th frame (step 30 is one picture every half second: a long story
    /// on one contact sheet instead of thousands of files). The saving
    /// happens outside the timer, so the timing stays honest.
    /// </summary>
    private static void SaveFrameIfAsked(FrameBuffer frame, double t)
    {
        string? ask = Environment.GetEnvironmentVariable("PECKWORKS_FRAMES");
        if (string.IsNullOrEmpty(ask)) return;      // unset, or set to "" (PowerShell cannot always unset a variable)
        string[] parts = ask.Split(';');
        double from = double.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture);
        int count = int.Parse(parts[2]);
        int step = parts.Length > 3 ? Math.Max(1, int.Parse(parts[3])) : 1;
        int n = (int)Math.Round((t - from) * 60);
        if (t < from - 1e-9 || n >= count || n % step != 0) return;
        Directory.CreateDirectory(parts[0]);
        using Bitmap bmp = frame.ToBitmap();
        bmp.Save(Path.Combine(parts[0], $"frame{n:D4}.png"), ImageFormat.Png);
    }
}
