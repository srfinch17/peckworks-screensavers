using Peckworks.Screensavers.Core;

namespace Sakura;

/// <summary>
/// Sakura's knobs. Saved under HKEY_CURRENT_USER\Software\Peckworks\Screensavers\Sakura.
/// The base class does the saving; the settings dialog makes one slider per knob.
/// </summary>
public sealed class SakuraSettings : ScreensaverSettings
{
    private readonly IntSetting _density, _speed, _wind, _size, _surprises;

    public SakuraSettings() : base("Sakura")
    {
        _density = Add(nameof(DensityPercent), "Petals", 20, 300, 100, "%");
        _speed = Add(nameof(FallSpeedPercent), "Fall speed", 25, 300, 100, "%");
        _wind = Add(nameof(WindPercent), "Breeze", 0, 300, 100, "%");
        _size = Add(nameof(PetalSizePercent), "Petal size", 50, 200, 100, "%");
        _surprises = Add(nameof(SurprisePercent), "Surprises", 0, 300, 100, "%");
    }

    /// <summary>How many petals are falling at once.</summary>
    public int DensityPercent => _density.Value;

    /// <summary>How fast petals fall. 100 = a slow, snow-like drift.</summary>
    public int FallSpeedPercent => _speed.Value;

    /// <summary>How hard the breeze pushes petals sideways. 0 = still air.</summary>
    public int WindPercent => _wind.Value;

    /// <summary>Petal size.</summary>
    public int PetalSizePercent => _size.Value;

    /// <summary>How often a happening (a kingfisher, a floating lantern...) comes on. 100 = one every 5 to 12 seconds, 0 = never.</summary>
    public int SurprisePercent => _surprises.Value;

    public static SakuraSettings LoadSaved()
    {
        var s = new SakuraSettings();
        s.Load();
        return s;
    }
}
