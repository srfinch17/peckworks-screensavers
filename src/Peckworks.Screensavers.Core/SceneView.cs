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
/// FEYNMAN VERSION of what it does, about 60 to 100 times a second:
///   1. A timer taps it on the shoulder ("tick").
///   2. It checks a stopwatch: how much real time passed since the last tick?
///   3. It tells the scene "move forward that much time" (Update).
///   4. It tells the scene "draw yourself on this sheet" (Render).
///   5. It slaps the sheet onto the screen (Present).
/// </summary>
public sealed class SceneView : Control
{
    private readonly Func<int, int, IScreensaverScene> _sceneFactory;

    // "System.Windows.Forms.Timer" (spelled out because .NET also has a
    // different Timer class in System.Threading) fires its Tick event ON the UI
    // thread, the same thread that owns the window. That keeps things simple:
    // no two pieces of code touch the scene at the same moment.
    private readonly System.Windows.Forms.Timer _timer;
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

    public SceneView(Func<int, int, IScreensaverScene> sceneFactory)
    {
        _sceneFactory = sceneFactory;

        // Tell WinForms: "I paint every pixel myself; never erase me to the
        // background color first." Without this, each repaint would flash
        // gray/black for an instant before our picture landed: flicker.
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                 ControlStyles.Opaque | ControlStyles.Selectable, true);
        BackColor = Color.Black;

        // Interval 10ms is the smallest Windows allows for this kind of timer.
        // In practice Windows' clock ticks every 15.6ms by default, so we get
        // about 64 frames per second, or 100 if something on the PC raised the
        // clock rate. Either is fine because Update uses real elapsed time.
        _timer = new System.Windows.Forms.Timer { Interval = 10 };
        _timer.Tick += (_, _) => Step();
    }

    /// <summary>Start playing.</summary>
    public void Start()
    {
        _clock.Restart();
        _lastTime = 0;
        _timer.Start();
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
    private void Step()
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
    }

    /// <summary>Make sure we have a scene and a frame buffer that match our current size.</summary>
    private bool EnsureScene()
    {
        int w = ClientSize.Width, h = ClientSize.Height;
        if (w <= 0 || h <= 0) return false;   // minimized or not laid out yet

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
        try { _frame.PresentTo(hdc); }
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
        try { _frame.PresentTo(hdc); }
        finally { e.Graphics.ReleaseHdc(hdc); }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Dispose();
            _scene?.Dispose();
        }
        base.Dispose(disposing);
    }
}
