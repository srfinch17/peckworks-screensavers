using Peckworks.Screensavers.Core;

namespace MatrixRain;

/// <summary>
/// The dialog Windows shows when you click "Settings..." in Screen Saver Settings.
///
/// It is built entirely in code (no drag-and-drop designer file), so everything
/// about it is visible right here. Layout: a live preview on top, four sliders
/// under it, and OK / Cancel / Defaults buttons at the bottom.
///
/// The sliders edit a DRAFT copy of the settings. The preview plays the draft
/// so you can judge by eye. Only OK saves the draft to the registry; Cancel
/// throws it away.
/// </summary>
internal sealed class SettingsForm : Form
{
    private readonly MatrixRainSettings _draft = MatrixRainSettings.Load();
    private readonly SceneView _preview;
    private readonly List<(TrackBar Bar, Action<int> Apply, Func<MatrixRainSettings, int> Read)> _sliders = [];

    // Moving a slider fires many change events per second. Rebuilding the preview
    // on every one would stutter, so we wait until the slider has been still for
    // a moment (this pattern is called "debouncing").
    private readonly System.Windows.Forms.Timer _debounce = new() { Interval = 150 };

    public SettingsForm()
    {
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

        Text = "Matrix Rain Settings";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Font = new Font("Segoe UI", 9f);
        BackColor = Color.FromArgb(18, 18, 18);
        ForeColor = Color.FromArgb(160, 255, 170);
        ClientSize = new Size(560, 560);

        // ---- Live preview ----
        // pixelScale = primary screen height / 1080 makes the preview show the
        // characters at their TRUE full-screen size (a cropped peek at the real
        // thing), which is what you want when choosing a character size.
        double trueScale = (Screen.PrimaryScreen?.Bounds.Height ?? 1080) / 1080.0;
        _preview = new SceneView((w, h) => new MatrixRainScene(w, h, trueScale, _draft))
        {
            Location = new Point(12, 12),
            Size = new Size(536, 300),
            PrewarmSeconds = 4,
        };
        Controls.Add(_preview);

        // ---- Sliders ----
        int y = 326;
        AddSlider("Speed", ref y, MatrixRainSettings.MinSpeed, MatrixRainSettings.MaxSpeed, "%",
            s => s.SpeedPercent, v => _draft.SpeedPercent = v);
        AddSlider("Density", ref y, MatrixRainSettings.MinDensity, MatrixRainSettings.MaxDensity, "%",
            s => s.DensityPercent, v => _draft.DensityPercent = v);
        AddSlider("Character size", ref y, MatrixRainSettings.MinCharacterSize, MatrixRainSettings.MaxCharacterSize, " px",
            s => s.CharacterSize, v => _draft.CharacterSize = v);
        AddSlider("Glow", ref y, MatrixRainSettings.MinGlow, MatrixRainSettings.MaxGlow, "%",
            s => s.GlowPercent, v => _draft.GlowPercent = v);

        // ---- Buttons ----
        var ok = MakeButton("OK", 290);
        var cancel = MakeButton("Cancel", 380);
        var defaults = MakeButton("Defaults", 470);
        ok.Click += (_, _) => { _draft.Save(); DialogResult = DialogResult.OK; Close(); };
        cancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
        defaults.Click += (_, _) => ResetToDefaults();
        AcceptButton = ok;       // Enter key = OK
        CancelButton = cancel;   // Esc key = Cancel

        _debounce.Tick += (_, _) => { _debounce.Stop(); _preview.RestartScene(); };

        ResumeLayout(false);   // step 4 of the DPI recipe above
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        _preview.Start();
    }

    private void AddSlider(string label, ref int y, int min, int max, string unit,
        Func<MatrixRainSettings, int> read, Action<int> apply)
    {
        var name = new Label { Text = label, Location = new Point(12, y + 6), AutoSize = true };
        var value = new Label { Location = new Point(496, y + 6), AutoSize = true };
        var bar = new TrackBar
        {
            Minimum = min,
            Maximum = max,
            Value = read(_draft),
            TickStyle = TickStyle.None,
            Location = new Point(120, y),
            Width = 370,
            BackColor = BackColor,
        };
        value.Text = bar.Value + unit;
        bar.ValueChanged += (_, _) =>
        {
            apply(bar.Value);
            value.Text = bar.Value + unit;
            _debounce.Stop();    // restart the "has it been still?" countdown
            _debounce.Start();
        };
        Controls.AddRange([name, bar, value]);
        _sliders.Add((bar, apply, read));
        y += 44;
    }

    private Button MakeButton(string text, int x)
    {
        var b = new Button
        {
            Text = text,
            Location = new Point(x, 516),
            Size = new Size(80, 30),
            FlatStyle = FlatStyle.Flat,
            ForeColor = ForeColor,
            BackColor = Color.FromArgb(30, 40, 30),
        };
        Controls.Add(b);
        return b;
    }

    private void ResetToDefaults()
    {
        var defaults = new MatrixRainSettings();
        foreach (var (bar, _, read) in _sliders)
            bar.Value = read(defaults);   // fires ValueChanged, which updates the draft
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _debounce.Dispose();
        base.Dispose(disposing);
    }
}
