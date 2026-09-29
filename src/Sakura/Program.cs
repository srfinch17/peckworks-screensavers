using Peckworks.Screensavers.Core;

namespace Sakura;

/// <summary>
/// Tells the shared engine what "Sakura" is: its name, how to build the
/// animation, and how to build its settings dialog. Everything else (full
/// screen, preview box, quitting on mouse move) the engine already knows.
/// </summary>
internal sealed class SakuraScreensaver : ScreensaverDefinition
{
    public override string DisplayName => "Sakura";

    public override IScreensaverScene CreateScene(int width, int height, bool isPreview) =>
        new SakuraScene(width, height, SakuraSettings.LoadSaved());

    public override Form CreateSettingsForm()
    {
        var draft = SakuraSettings.LoadSaved();
        return new SettingsDialog("Sakura Settings", draft,
            (w, h) => new SakuraScene(w, h, draft),
            back: Color.FromArgb(40, 30, 38),
            fore: Color.FromArgb(255, 205, 222),
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
        ScreensaverApp.Run(args, new SakuraScreensaver());
    }
}
