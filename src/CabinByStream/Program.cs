using Peckworks.Screensavers.Core;

namespace CabinByStream;

/// <summary>
/// Tells the shared engine what "Cabin by Stream" is: its name, how to build
/// the animation, and how to build its settings dialog. Everything else (full
/// screen, preview box, quitting on mouse move) the engine already knows.
/// </summary>
internal sealed class CabinByStreamScreensaver : ScreensaverDefinition
{
    public override string DisplayName => "Cabin by Stream";

    public override IScreensaverScene CreateScene(int width, int height, bool isPreview) =>
        new CabinByStreamScene(width, height, CabinByStreamSettings.LoadSaved());

    public override Form CreateSettingsForm()
    {
        var draft = CabinByStreamSettings.LoadSaved();
        return new SettingsDialog("Cabin by Stream Settings", draft,
            (w, h) => new CabinByStreamScene(w, h, draft),
            back: Color.FromArgb(30, 34, 52),
            fore: Color.FromArgb(255, 214, 160),
            prewarmSeconds: 6);   // a few seconds so the chimney already has a plume of smoke
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
        ScreensaverApp.Run(args, new CabinByStreamScreensaver());
    }
}
