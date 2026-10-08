using Peckworks.Screensavers.Core;

namespace CotswoldBrook;

/// <summary>
/// Tells the shared engine what "Cotswold Brook" is: its name, how to build
/// the animation, and how to build its settings dialog. Everything else (full
/// screen, preview box, quitting on mouse move) the engine already knows.
/// </summary>
internal sealed class CotswoldBrookScreensaver : ScreensaverDefinition
{
    public override string DisplayName => "Cotswold Brook";

    public override IScreensaverScene CreateScene(int width, int height, bool isPreview) =>
        new CotswoldBrookScene(width, height, CotswoldBrookSettings.LoadSaved());

    public override Form CreateSettingsForm()
    {
        var draft = CotswoldBrookSettings.LoadSaved();
        return new SettingsDialog("Cotswold Brook Settings", draft,
            (w, h) => new CotswoldBrookScene(w, h, draft),
            back: Color.FromArgb(38, 44, 58),
            fore: Color.FromArgb(240, 206, 150),
            prewarmSeconds: 6);   // a few seconds so the chimney already has a plume
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
        ScreensaverApp.Run(args, new CotswoldBrookScreensaver());
    }
}
