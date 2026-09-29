using Peckworks.Screensavers.Core;

namespace MatrixRain;

/// <summary>
/// Matrix Rain's knobs. The base class (Core/ScreensaverSettings.cs) handles
/// saving them in the registry and the settings dialog builds one slider per
/// knob automatically, so all this class does is declare them.
///
/// Saved under HKEY_CURRENT_USER\Software\Peckworks\Screensavers\MatrixRain.
/// </summary>
public sealed class MatrixRainSettings : ScreensaverSettings
{
    private readonly IntSetting _speed, _density, _size, _glow;

    public MatrixRainSettings() : base("MatrixRain")
    {
        _speed = Add(nameof(SpeedPercent), "Speed", 25, 300, 100, "%");
        _density = Add(nameof(DensityPercent), "Density", 20, 300, 100, "%");
        _size = Add(nameof(CharacterSize), "Character size", 12, 64, 28, " px");
        _glow = Add(nameof(GlowPercent), "Glow", 0, 200, 100, "%");
    }

    /// <summary>How fast the rain falls. 100 = the film's pace.</summary>
    public int SpeedPercent => _speed.Value;

    /// <summary>How many raindrops. 100 = the film's density.</summary>
    public int DensityPercent => _density.Value;

    /// <summary>
    /// Height of one character, in pixels, on a 1080-pixel-tall screen. On a
    /// taller screen (like 4K, 2160 pixels) it scales up proportionally, so the
    /// rain looks the same on every monitor.
    /// </summary>
    public int CharacterSize => _size.Value;

    /// <summary>How much glow. 0 = none, 100 = normal, 200 = heavy haze.</summary>
    public int GlowPercent => _glow.Value;

    /// <summary>A settings object filled with whatever the user last saved.</summary>
    public static MatrixRainSettings LoadSaved()
    {
        var s = new MatrixRainSettings();
        s.Load();
        return s;
    }
}
