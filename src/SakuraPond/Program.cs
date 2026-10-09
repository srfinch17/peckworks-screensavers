using Peckworks.Screensavers.Core;

namespace SakuraPond;

/// <summary>
/// Tells the shared engine what "Sakura Pond" is: its name, how to build the
/// animation, and how to build its settings dialog. Everything else (full
/// screen, preview box, quitting on mouse move) the engine already knows.
/// </summary>
internal sealed class SakuraPondScreensaver : ScreensaverDefinition
{
    public override string DisplayName => "Sakura Pond";

    // On a screen wider than this it draws at this width and Windows enlarges
    // the picture. The scene is soft and painterly, so 1440p detail enlarged
    // to 4K loses almost nothing, and drawing a 4K screen's eight million
    // pixels for every koi, petal and ripple would cost more than a frame has.
    public override int MaxRenderWidth => 2560;

    public override IScreensaverScene CreateScene(int width, int height, bool isPreview) =>
        new SakuraPondScene(width, height, SakuraPondSettings.LoadSaved());

    public override Form CreateSettingsForm()
    {
        var draft = SakuraPondSettings.LoadSaved();
        return new SettingsDialog("Sakura Pond Settings", draft,
            (w, h) => new SakuraPondScene(w, h, draft),
            back: Color.FromArgb(24, 40, 44),
            fore: Color.FromArgb(255, 205, 222),
            prewarmSeconds: 0);   // petals already float on the water at the start
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
        ScreensaverApp.Run(args, new SakuraPondScreensaver());
    }
}
