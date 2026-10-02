using Peckworks.Screensavers.Core;

namespace Christmas;

/// <summary>
/// Tells the shared engine what "Christmas" is: its name, how to build the
/// animation, and how to build its settings dialog. Everything else (full
/// screen, preview box, quitting on mouse move) the engine already knows.
/// </summary>
internal sealed class ChristmasScreensaver : ScreensaverDefinition
{
    public override string DisplayName => "Christmas";

    public override IScreensaverScene CreateScene(int width, int height, bool isPreview) =>
        new ChristmasScene(width, height, ChristmasSettings.LoadSaved());

    public override Form CreateSettingsForm()
    {
        var draft = ChristmasSettings.LoadSaved();
        return new SettingsDialog("Christmas Settings", draft,
            (w, h) => new ChristmasScene(w, h, draft),
            back: Color.FromArgb(16, 26, 58),
            fore: Color.FromArgb(214, 232, 255),
            prewarmSeconds: 0);   // snow already starts scattered across the screen
    }
}

internal static class Program
{
    /// <summary>
    /// Where the program starts. See MatrixRain/Program.cs for what
    /// [STAThread] and ApplicationConfiguration.Initialize() do; it is identical.
    /// </summary>
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        ScreensaverApp.Run(args, new ChristmasScreensaver());
    }
}
