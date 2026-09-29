using Peckworks.Screensavers.Core;

namespace SakuraDusk;

/// <summary>
/// Tells the shared engine what "Sakura Dusk" is: its name, how to build the
/// animation, and how to build its settings dialog. Everything else (full
/// screen, preview box, quitting on mouse move) the engine already knows.
/// </summary>
internal sealed class SakuraDuskScreensaver : ScreensaverDefinition
{
    public override string DisplayName => "Sakura Dusk";

    public override IScreensaverScene CreateScene(int width, int height, bool isPreview) =>
        new SakuraDuskScene(width, height, SakuraDuskSettings.LoadSaved());

    public override Form CreateSettingsForm()
    {
        var draft = SakuraDuskSettings.LoadSaved();
        return new SettingsDialog("Sakura Dusk Settings", draft,
            (w, h) => new SakuraDuskScene(w, h, draft),
            back: Color.FromArgb(44, 28, 48),
            fore: Color.FromArgb(255, 204, 168),
            prewarmSeconds: 0);   // petals already start scattered across the screen
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
        ScreensaverApp.Run(args, new SakuraDuskScreensaver());
    }
}
