namespace Peckworks.Screensavers.Core;

/// <summary>
/// The window the animation plays in. It comes in three flavors, picked by
/// the constructor you call:
///
///   FULL SCREEN (/s): no border, covers one whole monitor, sits on top of
///     everything, hides the mouse pointer, and closes the moment you move the
///     mouse, click, or press a key. That "wake up" rule is what makes it
///     behave like a screensaver rather than a video player.
///
///   PREVIEW (/p): adopted as a child INSIDE the little monitor picture in
///     Screen Saver Settings.
///
///   WINDOWED (/window): a normal resizable window for development, which does
///     NOT quit on mouse movement (so you can actually look at it).
/// </summary>
public sealed class ScreensaverWindow : Form
{
    private readonly SceneView _view;
    private readonly bool _closeOnInput;
    private readonly IntPtr _previewParent;

    // Where the mouse was when we first saw it. See OnViewMouseMove.
    private Point? _mouseStart;

    /// <summary>Full-screen window covering one monitor.</summary>
    public static ScreensaverWindow FullScreen(ScreensaverDefinition def, Rectangle monitorBounds)
    {
        var w = new ScreensaverWindow(def, isPreview: false, closeOnInput: true, IntPtr.Zero);
        w.FormBorderStyle = FormBorderStyle.None;  // no title bar, no edges
        w.StartPosition = FormStartPosition.Manual; // "I will place you myself"
        w.Bounds = monitorBounds;                   // exactly cover this monitor
        w.TopMost = true;                           // above the taskbar and all apps
        w.ShowInTaskbar = false;
        return w;
    }

    /// <summary>Normal resizable window, for development and judging by eye.</summary>
    public static ScreensaverWindow Windowed(ScreensaverDefinition def, Size clientSize)
    {
        var w = new ScreensaverWindow(def, isPreview: false, closeOnInput: false, IntPtr.Zero);
        w.Text = def.DisplayName + " (windowed test mode, Esc to close)";
        w.ClientSize = clientSize;
        w.StartPosition = FormStartPosition.CenterScreen;
        return w;
    }

    /// <summary>Child window living inside Windows' Screen Saver Settings preview box.</summary>
    public static ScreensaverWindow Preview(ScreensaverDefinition def, IntPtr parentHandle)
    {
        var w = new ScreensaverWindow(def, isPreview: true, closeOnInput: false, parentHandle);
        w.FormBorderStyle = FormBorderStyle.None;
        w.ShowInTaskbar = false;

        // A WinForms Form is normally a top-level window. Here we perform a
        // little adoption ceremony so it becomes a child of Windows' preview box:
        //  1. SetParent: "your parent is now that box".
        //  2. Add the WS_CHILD style flag: "behave like a child" (clip to the
        //     parent, move with it, die with it).
        //  3. Fill the parent's inside area exactly.
        NativeMethods.SetParent(w.Handle, parentHandle);
        long style = NativeMethods.GetWindowLongPtr(w.Handle, NativeMethods.GWL_STYLE).ToInt64();
        NativeMethods.SetWindowLongPtr(w.Handle, NativeMethods.GWL_STYLE, new IntPtr(style | NativeMethods.WS_CHILD));
        NativeMethods.GetClientRect(parentHandle, out var rect);
        w.Location = Point.Empty;
        w.Size = new Size(rect.Width, rect.Height);
        return w;
    }

    private ScreensaverWindow(ScreensaverDefinition def, bool isPreview, bool closeOnInput, IntPtr previewParent)
    {
        _closeOnInput = closeOnInput;
        _previewParent = previewParent;

        BackColor = Color.Black;
        AutoScaleMode = AutoScaleMode.None; // we work in real pixels, no automatic scaling
        KeyPreview = true;                  // the form hears key presses before child controls do

        _view = new SceneView((width, height) => def.CreateScene(width, height, isPreview))
        {
            Dock = DockStyle.Fill,          // fill the whole window
        };
        Controls.Add(_view);

        // The view covers the whole form, so mouse events land on the view,
        // not the form. We listen to the view's events.
        _view.MouseMove += OnViewMouseMove;
        _view.MouseDown += (_, _) => WakeUp();
        _view.MouseWheel += (_, _) => WakeUp();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        if (_closeOnInput)
        {
            Cursor.Hide();   // screensavers hide the pointer

            // The taskbar is itself an "always on top" window. Windows only
            // tucks it away when a window covering the whole monitor is the
            // ACTIVE window (the one receiving keyboard input). So we ask to be
            // active. Without this the taskbar stays drawn over the rain.
            Activate();
            NativeMethods.SetForegroundWindow(Handle);
        }
        _view.Focus();
        _view.Start();

        // In preview mode, check twice a second that the Settings dialog is
        // still open. When the user closes it or picks a different
        // screensaver, Windows destroys the preview box; we should then quit
        // instead of lingering invisibly in memory.
        if (_previewParent != IntPtr.Zero)
        {
            var watchdog = new System.Windows.Forms.Timer { Interval = 500 };
            watchdog.Tick += (_, _) =>
            {
                if (!NativeMethods.IsWindow(_previewParent)) Application.Exit();
            };
            watchdog.Start();
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (_closeOnInput || e.KeyCode == Keys.Escape) WakeUp(force: true);
    }

    /// <summary>
    /// Quit if the mouse genuinely moved.
    ///
    /// Why not just quit on the FIRST MouseMove event? Because Windows sends a
    /// MouseMove the moment our window appears under the pointer, even though
    /// nobody touched anything. A desk bump can also nudge a mouse by 1 pixel.
    /// So: remember where the pointer was the first time, and only quit once it
    /// has traveled more than a few pixels from there.
    /// </summary>
    private void OnViewMouseMove(object? sender, MouseEventArgs e)
    {
        if (!_closeOnInput) return;
        Point now = Cursor.Position; // screen coordinates, same on every monitor window
        if (_mouseStart is not Point start)
        {
            _mouseStart = now;
            return;
        }
        int dx = now.X - start.X, dy = now.Y - start.Y;
        if (dx * dx + dy * dy > 10 * 10) WakeUp();
    }

    private void WakeUp(bool force = false)
    {
        if (!_closeOnInput && !force) return;
        // Application.Exit closes EVERY window (one per monitor) and ends the program.
        Application.Exit();
    }
}
