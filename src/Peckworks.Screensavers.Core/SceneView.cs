using System.Diagnostics;

namespace Peckworks.Screensavers.Core;

/// <summary>
/// A rectangle on screen that plays a scene, like a little TV.
///
/// It is used in three places: the full-screen screensaver window, the tiny
/// preview in Windows' Screen Saver Settings, and the live preview inside our own
/// settings dialog. All three are "a box that plays the animation"; only the
/// box around it differs, so the playing part lives here once.
///
/// FEYNMAN VERSION of what it does, once per screen refresh (60 times a
/// second on most screens):
///   1. The frame pump (FramePump, below) taps it on the shoulder.
///   2. It checks a stopwatch: how much real time passed since the last frame?
///   3. It tells the scene "move forward that much time" (Update).
///   4. It tells the scene "draw yourself on this sheet" (Render).
///   5. It slaps the sheet onto the screen (Present).
/// </summary>
public sealed class SceneView : Control
{
    private readonly Func<int, int, IScreensaverScene> _sceneFactory;

    private readonly Stopwatch _clock = new();

    private IScreensaverScene? _scene;
    private FrameBuffer? _frame;
    private double _lastTime;

    /// <summary>
    /// Seconds of animation to "fast forward" silently when a scene is created,
    /// so it starts mid-action instead of from an empty screen. The real
    /// screensaver uses 0 (the rain falling in from the top is part of the
    /// show); the settings dialog preview uses a few seconds.
    /// </summary>
    /// <remarks>
    /// The attribute tells the Visual Studio form designer "do not try to save
    /// this property into designer code". We build everything in code, but the
    /// compiler insists every public property on a Control says so.
    /// </remarks>
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public double PrewarmSeconds { get; set; }

    /// <summary>
    /// The widest the scene is drawn. On a wider screen the scene is drawn
    /// this wide (and as tall as keeps the screen's shape) and stretched to
    /// fill the view. See ScreensaverDefinition.MaxRenderWidth.
    /// </summary>
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public int MaxRenderWidth { get; set; } = int.MaxValue;

    public SceneView(Func<int, int, IScreensaverScene> sceneFactory)
    {
        _sceneFactory = sceneFactory;

        // Tell WinForms: "I paint every pixel myself; never erase me to the
        // background color first." Without this, each repaint would flash
        // gray/black for an instant before our picture landed: flicker.
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                 ControlStyles.Opaque | ControlStyles.Selectable, true);
        BackColor = Color.Black;

    }

    /// <summary>Start playing.</summary>
    public void Start()
    {
        _clock.Restart();
        _lastTime = 0;
        FramePump.Add(this);
    }

    /// <summary>Throw away the current scene and build a new one (for example after a setting changed).</summary>
    public void RestartScene()
    {
        _scene?.Dispose();
        _scene = null;
        _frame = null;
        EnsureScene();
        Invalidate();
    }

    /// <summary>One frame: update, render, present.</summary>
    internal void Step()
    {
        if (!EnsureScene()) return;

        double now = _clock.Elapsed.TotalSeconds;
        double elapsed = now - _lastTime;
        _lastTime = now;

        // If the PC stalled (say, 2 seconds while waking up), do not let the
        // world jump 2 seconds in one step; things would teleport. Cap it.
        elapsed = Math.Min(elapsed, 0.1);

        _scene!.Update(elapsed);
        _scene.Render(_frame!);
        Present();
        LogFrame(now);
    }

    // A testing aid for smoothness: with PECKWORKS_FRAMELOG set to a file
    // path, the first 300 frame times (milliseconds between frames) are
    // written there. Even spacing = smooth; a frame now and then twice as
    // long as the rest = a judder the eye sees.
    private static readonly string? FrameLogPath = Environment.GetEnvironmentVariable("PECKWORKS_FRAMELOG");
    private readonly List<double> _frameTimes = [];
    private void LogFrame(double now)
    {
        if (FrameLogPath is null || _frameTimes.Count > 300) return;
        _frameTimes.Add(now * 1000);
        if (_frameTimes.Count == 301)
            File.WriteAllLines(FrameLogPath, _frameTimes.Zip(_frameTimes.Skip(1), (a, b) => (b - a).ToString("F2")));
    }

    /// <summary>Make sure we have a scene and a frame buffer that match our current size.</summary>
    private bool EnsureScene()
    {
        int w = ClientSize.Width, h = ClientSize.Height;
        if (w <= 0 || h <= 0) return false;   // minimized or not laid out yet
        if (w > MaxRenderWidth)
        {
            // Draw smaller, keeping the screen's shape; Present stretches it.
            h = Math.Max(1, (int)Math.Round(h * (double)MaxRenderWidth / w));
            w = MaxRenderWidth;
        }

        if (_scene != null && _frame != null && _frame.Width == w && _frame.Height == h)
            return true;

        // Size changed (or first time): rebuild for the new size.
        _scene?.Dispose();
        _scene = _sceneFactory(w, h);
        _frame = new FrameBuffer(w, h);

        // Fast forward, in small steps, without drawing.
        for (double t = 0; t < PrewarmSeconds; t += 1.0 / 30)
            _scene.Update(1.0 / 30);

        _scene.Render(_frame);
        return true;
    }

    /// <summary>Copy the finished frame onto the screen.</summary>
    private void Present()
    {
        if (_frame == null || !IsHandleCreated) return;

        // CreateGraphics gives us a drawing surface for this control right now.
        // GetHdc digs out the raw Windows handle (HDC) underneath it, which is
        // what the fast SetDIBitsToDevice call needs.
        using Graphics g = CreateGraphics();
        IntPtr hdc = g.GetHdc();
        try { _frame.PresentTo(hdc, ClientSize.Width, ClientSize.Height); }
        finally { g.ReleaseHdc(hdc); }
    }

    /// <summary>
    /// Windows sends "paint yourself" when part of us was covered and uncovered.
    /// We just re-show the last frame.
    /// </summary>
    protected override void OnPaint(PaintEventArgs e)
    {
        if (!EnsureScene() || _frame == null) return;
        IntPtr hdc = e.Graphics.GetHdc();
        try { _frame.PresentTo(hdc, ClientSize.Width, ClientSize.Height); }
        finally { e.Graphics.ReleaseHdc(hdc); }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            FramePump.Remove(this);
            _scene?.Dispose();
        }
        base.Dispose(disposing);
    }
}

/// <summary>
/// Drives every playing SceneView, one frame per screen refresh.
///
/// FEYNMAN VERSION: the screen redraws itself 60 times a second, like the
/// pages of a flip-book turning at a steady rate. Our first version used a
/// timer that ticked about 64 times a second, out of step with the pages: a
/// few times a second one page got two of our drawings (one was never seen)
/// or the same drawing twice. Something moving steadily, like a river, then
/// jerks a few times a second. Now we draw a frame, then WAIT for the screen
/// to turn its page (DwmFlush), then draw the next: one drawing per page.
///
/// It runs whenever the program has nothing else to do ("Application.Idle"),
/// and gives way the instant a message arrives (a mouse move must still
/// close the screensaver at once), then carries on.
///
/// If waiting for the page does not work on some PC (it returns at once, as
/// it can when Windows is not composing the desktop), we fall back to a
/// stopwatch: sleep until a sixtieth of a second has passed.
/// </summary>
internal static class FramePump
{
    private static readonly List<SceneView> Views = [];
    private static readonly Stopwatch Clock = Stopwatch.StartNew();
    private static double _lastFrame;
    private static int _quickFlushes;     // how many times in a row DwmFlush returned suspiciously fast

    public static void Add(SceneView view)
    {
        if (Views.Contains(view)) return;
        if (Views.Count == 0)
        {
            NativeMethods.timeBeginPeriod(1);   // so the stopwatch fallback's short sleeps are accurate
            Application.Idle += Run;
        }
        Views.Add(view);
    }

    public static void Remove(SceneView view)
    {
        Views.Remove(view);
        if (Views.Count == 0) Application.Idle -= Run;
    }

    private static void Run(object? sender, EventArgs e)
    {
        // Keep making frames for as long as no message is waiting.
        while (Views.Count > 0 && !NativeMethods.PeekMessage(out _, IntPtr.Zero, 0, 0, 0))
        {
            foreach (SceneView view in Views.ToArray())
                if (!view.IsDisposed && view.IsHandleCreated && view.Visible) view.Step();
            WaitForNextRefresh();
        }
    }

    private static void WaitForNextRefresh()
    {
        double before = Clock.Elapsed.TotalMilliseconds;
        if (_quickFlushes < 30)
        {
            bool ok = NativeMethods.DwmFlush() == 0;
            double waited = Clock.Elapsed.TotalMilliseconds - before;
            // A real wait for the next page takes a few milliseconds at least;
            // returning at once over and over means it is not really waiting.
            _quickFlushes = ok && waited > 1.0 ? 0 : _quickFlushes + 1;
            if (ok && waited > 1.0) { _lastFrame = Clock.Elapsed.TotalMilliseconds; return; }
        }
        // The fallback: wait until a sixtieth of a second since the last frame.
        while (Clock.Elapsed.TotalMilliseconds - _lastFrame < 1000.0 / 60 - 0.5)
            Thread.Sleep(1);
        _lastFrame = Clock.Elapsed.TotalMilliseconds;
    }
}
