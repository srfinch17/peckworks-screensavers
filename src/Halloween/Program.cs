using Peckworks.Screensavers.Core;

namespace Halloween;

/// <summary>
/// Tells the shared engine what "Halloween" is: its name, how to build the
/// animation, and how to build its settings dialog. Everything else (full
/// screen, preview box, quitting on mouse move) the engine already knows.
/// </summary>
internal sealed class HalloweenScreensaver : ScreensaverDefinition
{
    public override string DisplayName => "Halloween";

    public override IScreensaverScene CreateScene(int width, int height, bool isPreview) =>
        new HalloweenScene(width, height, HalloweenSettings.LoadSaved());

    public override Form CreateSettingsForm()
    {
        var draft = HalloweenSettings.LoadSaved();
        return new SettingsDialog("Halloween Settings", draft,
            (w, h) => new HalloweenScene(w, h, draft),
            back: Color.FromArgb(26, 16, 40),
            fore: Color.FromArgb(255, 170, 70),
            prewarmSeconds: 0);   // leaves already start scattered across the screen
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
        ScreensaverApp.Run(args, new HalloweenScreensaver());
    }
}
