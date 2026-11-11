namespace Peckworks.Screensavers.Core;

/// <summary>
/// The dialog Windows shows when you click "Settings..." in Screen Saver
/// Settings. Any screensaver can use it: hand it a settings object and a way to
/// build a preview scene, and it builds itself: a live preview on top, one
/// slider per setting, then OK / Cancel / Defaults.
///
/// It is built entirely in code (no drag-and-drop designer file), so everything
/// about it is visible right here.
///
/// The sliders edit a DRAFT settings object. The preview plays the draft so
/// you can judge by eye. Only OK saves the draft to the registry; Cancel
/// throws it away.
/// </summary>
public sealed class SettingsDialog : Form
{
    private readonly ScreensaverSettings _draft;
    private readonly SceneView _preview;
    private readonly List<(TrackBar Bar, IntSetting Setting)> _sliders = [];

    // Moving a slider fires many change events per second. Rebuilding the preview
    // on every one would stutter, so we wait until the slider has been still for
    // a moment before rebuilding (this pattern is called "debouncing").
    private readonly System.Windows.Forms.Timer _debounce = new() { Interval = 150 };

    /// <param name="title">Window title.</param>
    /// <param name="draft">Settings to edit. The preview factory should read from this same object.</param>
    /// <param name="previewFactory">Builds a scene for the preview, given its width and height.</param>
    /// <param name="back">Dialog background color.</param>
    /// <param name="fore">Dialog text color.</param>
    /// <param name="prewarmSeconds">How far to fast-forward the preview so it starts mid-action.</param>
    public SettingsDialog(string title, ScreensaverSettings draft,
        Func<int, int, IScreensaverScene> previewFactory, Color back, Color fore, double prewarmSeconds = 4)
    {
        _draft = draft;

        // HIGH-DPI SCALING. Every position and size below is written for a
        // screen at 100% scaling, which Windows calls 96 DPI (dots per inch).
        // On a screen at 250% (like a 4K laptop) everything must be 2.5x bigger
        // or the dialog would be tiny and the text would not fit.
        //
        // The recipe: (1) SuspendLayout, "hold still while I set things up";
        // (2) say "I designed this at 96 DPI" and "scale by DPI"; (3) add all
        // the controls; (4) ResumeLayout. WinForms then multiplies every
        // position and size by (actual DPI / 96) in one go. Skip step 2 and the
        // text grows while the layout does not, and everything overlaps.
        SuspendLayout();
        AutoScaleDimensions = new SizeF(96f, 96f);
        AutoScaleMode = AutoScaleMode.Dpi;

        Text = title;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Font = new Font("Segoe UI", 9f);
        BackColor = back;
        ForeColor = fore;

        // ---- Live preview ----
        _preview = new SceneView(previewFactory)
        {
            Location = new Point(12, 12),
            Size = new Size(536, 300),
            PrewarmSeconds = prewarmSeconds,
        };
        Controls.Add(_preview);

        // ---- One slider per setting, in a box that SCROLLS when there are too many to fit ----
        // Think of a long shopping list in a short window: you do not make the window taller than the
        // wall, you let the list slide up and down inside it. The sliders live in their own box (a
        // Panel). If they all fit on the screen the box is exactly as tall as the list and the dialog
        // looks as it always did. If they do not (the pond has 24 sliders; at 250% scaling the screen
        // has room for about nine: the last sliders AND the OK button were below the bottom of the
        // screen, out of reach), the box stops at the room there is and grows a scroll bar.
        // WinForms does the scrolling itself: AutoScroll = true is the whole trick.
        var knobs = new Panel { Location = new Point(0, 318), AutoScroll = true, BackColor = back };
        int y = 8;
        foreach (IntSetting s in draft.All)
        {
            AddSlider(knobs, s, y);
            y += 44;
        }
        // The room for the box, in the same 100%-scaling units as everything here: the screen's
        // working height (the part not under the taskbar) divided back down by the scaling, less the
        // preview above (318), the buttons below (56) and the title bar and a margin (48). Never
        // fewer than three rows, however small the screen.
        int screenHeight = (Screen.PrimaryScreen ?? Screen.AllScreens[0]).WorkingArea.Height;
        int room = Math.Max(3 * 44 + 8, (int)(screenHeight * 96f / DeviceDpi) - 318 - 56 - 48);
        knobs.Size = new Size(560, Math.Min(y, room));
        Controls.Add(knobs);

        // ---- Buttons ----
        int buttonY = knobs.Bottom + 12;
        var ok = MakeButton("OK", 290, buttonY);
        var cancel = MakeButton("Cancel", 380, buttonY);
        var defaults = MakeButton("Defaults", 470, buttonY);
        ok.Click += (_, _) => { _draft.Save(); DialogResult = DialogResult.OK; Close(); };
        cancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
        defaults.Click += (_, _) =>
        {
            foreach (var (bar, setting) in _sliders)
                bar.Value = setting.Default;   // fires ValueChanged, which updates the draft
        };
        AcceptButton = ok;       // Enter key = OK
        CancelButton = cancel;   // Esc key = Cancel

        ClientSize = new Size(560, buttonY + 44);

        _debounce.Tick += (_, _) => { _debounce.Stop(); _preview.RestartScene(); };

        ResumeLayout(false);   // step 4 of the DPI recipe above
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        _preview.Start();
    }

    private void AddSlider(Panel box, IntSetting setting, int y)
    {
        var name = new Label { Text = setting.Label, Location = new Point(12, y + 6), AutoSize = true };
        var value = new Label { Location = new Point(496, y + 6), AutoSize = true };
        var bar = new TrackBar
        {
            Minimum = setting.Min,
            Maximum = setting.Max,
            Value = setting.Value,
            TickStyle = TickStyle.None,
            Location = new Point(120, y),
            Width = 370,
            BackColor = BackColor,
        };
        value.Text = bar.Value + setting.Unit;
        bar.ValueChanged += (_, _) =>
        {
            setting.Value = bar.Value;
            value.Text = bar.Value + setting.Unit;
            _debounce.Stop();    // restart the "has it been still?" countdown
            _debounce.Start();
        };
        box.Controls.AddRange([name, bar, value]);
        _sliders.Add((bar, setting));
    }

    private Button MakeButton(string text, int x, int y)
    {
        var b = new Button
        {
            Text = text,
            Location = new Point(x, y),
            Size = new Size(80, 30),
            FlatStyle = FlatStyle.Flat,
            ForeColor = ForeColor,
            // A button color slightly different from the background, so it reads as a button.
            BackColor = ControlPaint.Light(BackColor, 0.15f),
        };
        Controls.Add(b);
        return b;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _debounce.Dispose();
        base.Dispose(disposing);
    }
}
