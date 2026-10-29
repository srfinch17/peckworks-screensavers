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
    // TWO SHEETS AND A PRESENT THREAD (2026-10-29, round forty's review). Drawing a frame and copying it to the screen
    // used to happen one after the other on one thread, so on a 4K screen the frame cost was the painting (about 13 ms
    // at the 1920 draw width) PLUS the stretch to the screen (about 4 ms): 17 ms, past a page, and the pump dropped to
    // an even 30. Now there are two sheets: the scene paints the next frame on one while a second thread copies the
    // finished one to the screen, so the stretch hides behind the painting and the frame costs what the painting costs.
    // FEYNMAN VERSION: one person paints a picture while another carries the previous picture to the gallery, instead
    // of the painter walking it over and coming back before touching the next canvas.
    private readonly FrameBuffer?[] _frames = new FrameBuffer?[2];
    private int _front;                                              // _frames[_front] is the finished frame (being shown); the other is painted into
    private Thread? _presenter;
    private readonly AutoResetEvent _present = new(false);           // "a new front frame is ready"
    private readonly ManualResetEventSlim _idle = new(true);         // "the presenter is not reading any sheet"
    private readonly object _blit = new();                           // one copy to the window at a time (the presenter and OnPaint)
    private volatile bool _stop;
    private IntPtr _hwnd;                                            // the window's handle, read on the UI thread, used by the presenter
    private volatile int _clientW, _clientH;                         // the window's size, likewise
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
        if (_presenter == null)
        {
            _presenter = new Thread(PresentLoop) { IsBackground = true, Name = "present" };
            _presenter.Start();
        }
        FramePump.Add(this);
    }

    /// <summary>Throw away the current scene and build a new one (for example after a setting changed).</summary>
    public void RestartScene()
    {
        _idle.Wait();                 // the presenter is not reading a sheet we are about to drop
        _scene?.Dispose();
        _scene = null;
        _frames[0] = _frames[1] = null;
        EnsureScene();
        Invalidate();
    }

    /// <summary>One frame: update, render, and hand the finished sheet to the present thread.</summary>
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
        int back = 1 - _front;
        _scene.Render(_frames[back]!);
        // Hand it over: wait (nearly never) for the presenter to finish with the last sheet, swap, and wake it.
        _idle.Wait();
        _front = back;
        _hwnd = IsHandleCreated ? Handle : IntPtr.Zero;
        _clientW = ClientSize.Width; _clientH = ClientSize.Height;
        _idle.Reset();
        _present.Set();
        LogFrame(now);
    }

    /// <summary>The present thread: each time a frame is handed over, copies it to the window, then says it is idle.</summary>
    private void PresentLoop()
    {
        while (!_stop)
        {
            _present.WaitOne();
            if (_stop) break;
            try
            {
                FrameBuffer? frame = _frames[_front];
                IntPtr hwnd = _hwnd;
                if (frame != null && hwnd != IntPtr.Zero)
                    lock (_blit)
                    {
                        IntPtr hdc = NativeMethods.GetDC(hwnd);
                        if (hdc != IntPtr.Zero)
                        {
                            try { frame.PresentTo(hdc, _clientW, _clientH); }
                            finally { NativeMethods.ReleaseDC(hwnd, hdc); }
                        }
                    }
            }
            catch (Exception) { }   // a window going away under us: the next frame, or the end, sorts it out
            finally { _idle.Set(); }
        }
        _idle.Set();
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

        FrameBuffer? front = _frames[_front];
        if (_scene != null && front != null && front.Width == w && front.Height == h)
            return true;

        // Size changed (or first time): rebuild for the new size.
        _idle.Wait();                 // not while the presenter reads a sheet
        _scene?.Dispose();
        _scene = _sceneFactory(w, h);
        _frames[0] = new FrameBuffer(w, h);
        _frames[1] = new FrameBuffer(w, h);
        _front = 0;

        // Fast forward, in small steps, without drawing.
        for (double t = 0; t < PrewarmSeconds; t += 1.0 / 30)
            _scene.Update(1.0 / 30);

        _scene.Render(_frames[0]);
        return true;
    }

    /// <summary>
    /// Windows sends "paint yourself" when part of us was covered and uncovered.
    /// We just re-show the last finished frame (never at the same time as the present thread).
    /// </summary>
    protected override void OnPaint(PaintEventArgs e)
    {
        if (!EnsureScene()) return;
        FrameBuffer? frame = _frames[_front];
        if (frame == null) return;
        lock (_blit)
        {
            IntPtr hdc = e.Graphics.GetHdc();
            try { frame.PresentTo(hdc, ClientSize.Width, ClientSize.Height); }
            finally { e.Graphics.ReleaseHdc(hdc); }
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            FramePump.Remove(this);
            _stop = true;
            _present.Set();           // wake the presenter so it sees the stop
            _idle.Wait(500);          // let a copy in progress finish before the window goes
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

    // STEADY BEATS. A frame that takes longer than one page of the flip-book
    // (about 16.7 ms on a 60 Hz screen) misses its page and lands on the
    // next: 33 ms. A scene that takes 15 ms one frame and 18 the next then
    // shows at 16, 33, 16, 33: the eye reads that unevenness as a stutter,
    // worse than an even 30 frames a second. So the pump watches how long
    // recent frames took (drawing plus showing), and when they are too close
    // to the page time to make it reliably, it waits for TWO pages per frame,
    // on purpose, every frame: an even 30. When frames get quick again it
    // goes back to one page per frame. The scene is told the real time that
    // passed either way, so nothing moves slower, it only moves in bigger steps.
    private const double PageMs = 1000.0 / 60;
    private static double _costAvg;       // a running average of recent frame costs, ms
    private static bool _halfRate;        // waiting two pages per frame now?
    private static int _since;            // frames since the rate last changed (no flip-flopping)

    /// <summary>Two pages per frame right now? (For the frame log and tests.)</summary>
    public static bool HalfRate => _halfRate;

    private static void Run(object? sender, EventArgs e)
    {
        // Keep making frames for as long as no message is waiting.
        while (Views.Count > 0 && !NativeMethods.PeekMessage(out _, IntPtr.Zero, 0, 0, 0))
        {
            double start = Clock.Elapsed.TotalMilliseconds;
            foreach (SceneView view in Views.ToArray())
                if (!view.IsDisposed && view.IsHandleCreated && view.Visible) view.Step();
            double cost = Clock.Elapsed.TotalMilliseconds - start;
            _costAvg += (Math.Min(cost, 100) - _costAvg) * 0.1;      // the last ten frames or so count most
            _since++;
            // Go to two pages a frame when frames cannot make one page with a
            // little to spare; come back only once they are clearly quick
            // again, and never flip within half a second of the last change.
            // Drop to an even 30 when the average cost nears a page (a frame that misses its page every other time is
            // the judder the eye sees), and come back once it sits under 78 percent of a page for a second and a half.
            // The way back was 62 percent for four seconds: on a 4K screen at the 2048 draw width the cost sits at 10 to
            // 11 ms, so one slow patch (the first frames, a duck visit) locked the pump at 30 fps for the rest of the run
            // (measured 2026-10-20: 299 of 300 frames at 33 ms with the cost long back at 11).
            if (!_halfRate && _costAvg > PageMs * 0.9 && _since > 30) { _halfRate = true; _since = 0; }
            else if (_halfRate && _costAvg < PageMs * 0.78 && _since > 45) { _halfRate = false; _since = 0; }
            WaitForNextRefresh();
            // Two pages a frame means two pages FROM THE FRAME'S START, not two waits after the work: a frame whose
            // work itself ran past a page (17 ms on a 4K screen, round forty) had its first wait land on the second
            // page and its second on the third, 50 ms a frame, a steady 20 instead of the even 30 this is for.
            if (_halfRate && Clock.Elapsed.TotalMilliseconds - start < PageMs * 1.5) WaitForNextRefresh();
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
        while (Clock.Elapsed.TotalMilliseconds - _lastFrame < PageMs - 0.5)
            Thread.Sleep(1);
        _lastFrame = Clock.Elapsed.TotalMilliseconds;
    }
}
