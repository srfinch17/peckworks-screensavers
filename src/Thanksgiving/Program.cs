using Peckworks.Screensavers.Core;

namespace Thanksgiving;

/// <summary>
/// Tells the shared engine what "Thanksgiving" is: its name, how to build the
/// animation, and how to build its settings dialog. Everything else (full
/// screen, preview box, quitting on mouse move) the engine already knows.
/// </summary>
internal sealed class ThanksgivingScreensaver : ScreensaverDefinition
{
    public override string DisplayName => "Thanksgiving";

    public override IScreensaverScene CreateScene(int width, int height, bool isPreview) =>
        new ThanksgivingScene(width, height, ThanksgivingSettings.LoadSaved());

    public override Form CreateSettingsForm()
    {
        var draft = ThanksgivingSettings.LoadSaved();
        return new SettingsDialog("Thanksgiving Settings", draft,
            (w, h) => new ThanksgivingScene(w, h, draft),
            back: Color.FromArgb(58, 30, 36),
            fore: Color.FromArgb(255, 222, 170));
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
        ScreensaverApp.Run(args, new ThanksgivingScreensaver());
    }
}
