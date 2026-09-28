using Microsoft.Win32;

namespace MatrixRain;

/// <summary>
/// Every knob the user can turn, plus where they are saved.
///
/// WHERE SETTINGS LIVE: the Windows Registry, a built-in database of settings
/// that every program can use. It is organized like folders ("keys") holding
/// named values. We use HKEY_CURRENT_USER\Software\Peckworks\Screensavers\MatrixRain.
/// "Current user" means each person who logs into the PC gets their own copy.
///
/// Why the registry and not a file next to the .scr? Because the .scr gets
/// installed into C:\Windows\System32, and normal programs are (rightly) not
/// allowed to write files there. The registry's per-user area is always writable.
///
/// You can look at the saved values yourself: press Win+R, type regedit, and
/// browse to the path above.
/// </summary>
public sealed class MatrixRainSettings
{
    private const string RegistryPath = @"Software\Peckworks\Screensavers\MatrixRain";

    /// <summary>How fast the rain falls. 100 = the film's pace.</summary>
    public int SpeedPercent { get; set; } = 100;

    /// <summary>How many raindrops. 100 = the film's density.</summary>
    public int DensityPercent { get; set; } = 100;

    /// <summary>
    /// Height of one character, in pixels, on a 1080-pixel-tall screen. On a
    /// taller screen (like 4K, 2160 pixels) it scales up proportionally, so the
    /// rain looks the same on every monitor.
    /// </summary>
    public int CharacterSize { get; set; } = 28;

    /// <summary>How much glow. 0 = none, 100 = normal, 200 = heavy haze.</summary>
    public int GlowPercent { get; set; } = 100;

    // Allowed ranges, used by the settings dialog sliders and to sanitize
    // anything odd found in the registry.
    public const int MinSpeed = 25, MaxSpeed = 300;
    public const int MinDensity = 20, MaxDensity = 300;
    public const int MinCharacterSize = 12, MaxCharacterSize = 64;
    public const int MinGlow = 0, MaxGlow = 200;

    /// <summary>Makes an independent copy (so the dialog can edit a draft without touching the original).</summary>
    public MatrixRainSettings Clone() => (MatrixRainSettings)MemberwiseClone();

    /// <summary>Reads saved settings, falling back to the defaults for anything missing.</summary>
    public static MatrixRainSettings Load()
    {
        var s = new MatrixRainSettings();
        try
        {
            // "using" guarantees the key is closed again when we are done,
            // like a door that shuts itself behind you.
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RegistryPath);
            if (key != null)
            {
                s.SpeedPercent = ReadInt(key, nameof(SpeedPercent), s.SpeedPercent, MinSpeed, MaxSpeed);
                s.DensityPercent = ReadInt(key, nameof(DensityPercent), s.DensityPercent, MinDensity, MaxDensity);
                s.CharacterSize = ReadInt(key, nameof(CharacterSize), s.CharacterSize, MinCharacterSize, MaxCharacterSize);
                s.GlowPercent = ReadInt(key, nameof(GlowPercent), s.GlowPercent, MinGlow, MaxGlow);
            }
        }
        catch
        {
            // A screensaver must never crash over a settings problem. If the
            // registry cannot be read for any reason, just use the defaults.
        }
        return s;
    }

    /// <summary>Writes the settings to the registry (creating the key the first time).</summary>
    public void Save()
    {
        using RegistryKey key = Registry.CurrentUser.CreateSubKey(RegistryPath);
        key.SetValue(nameof(SpeedPercent), SpeedPercent, RegistryValueKind.DWord);
        key.SetValue(nameof(DensityPercent), DensityPercent, RegistryValueKind.DWord);
        key.SetValue(nameof(CharacterSize), CharacterSize, RegistryValueKind.DWord);
        key.SetValue(nameof(GlowPercent), GlowPercent, RegistryValueKind.DWord);
    }

    private static int ReadInt(RegistryKey key, string name, int fallback, int min, int max) =>
        key.GetValue(name) is int v ? Math.Clamp(v, min, max) : fallback;
}
