using Peckworks.Screensavers.Core;

namespace MatrixRain;

/// <summary>
/// Tells the shared engine what "Matrix Rain" is: its name, how to build the
/// animation, and how to build its settings dialog. Everything else (full
/// screen, preview box, quitting on mouse move) the engine already knows.
/// </summary>
internal sealed class MatrixRainScreensaver : ScreensaverDefinition
{
    public override string DisplayName => "Matrix Rain";

    public override IScreensaverScene CreateScene(int width, int height, bool isPreview)
    {
        // Scale characters with the surface height, so a 4K monitor gets
        // characters twice as many pixels tall as a 1080p one and the rain
        // looks the same on both. The tiny Settings preview box gets a gentler
        // scale so the characters stay big enough to read as characters.
        double scale = isPreview ? Math.Max(0.30, height / 1080.0) : height / 1080.0;
        return new MatrixRainScene(width, height, scale, MatrixRainSettings.Load());
    }

    public override Form CreateSettingsForm() => new SettingsForm();
}

internal static class Program
{
    /// <summary>
    /// Where the program starts.
    ///
    /// [STAThread] ("Single-Threaded Apartment") is a required label for any
    /// Windows Forms program. It promises Windows that all our window code runs
    /// on one thread, which the old Windows UI plumbing depends on.
    ///
    /// args holds whatever came on the command line, for example "/s" or
    /// "/p 12345". See Core/CommandLine.cs for what each one means.
    /// </summary>
    [STAThread]
    private static void Main(string[] args)
    {
        // Turns on the DPI setting from the .csproj, modern-looking buttons, and
        // sharper default text. Must run before any window is created.
        ApplicationConfiguration.Initialize();

        ScreensaverApp.Run(args, new MatrixRainScreensaver());
    }
}
