using Peckworks.Screensavers.Core;

namespace SakuraDusk;

/// <summary>
/// Sakura Dusk's knobs, the same four as Sakura. Saved under
/// HKEY_CURRENT_USER\Software\Peckworks\Screensavers\SakuraDusk, so the two
/// screensavers keep separate settings. The base class does the saving; the
/// settings dialog makes one slider per knob.
/// </summary>
public sealed class SakuraDuskSettings : ScreensaverSettings
{
    private readonly IntSetting _density, _speed, _wind, _size, _surprises;

    public SakuraDuskSettings() : base("SakuraDusk")
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

    /// <summary>How often a happening (a floating lantern, a frog, the first star...) comes on. 100 = one every 5 to 12 seconds, 0 = never.</summary>
    public int SurprisePercent => _surprises.Value;

    public static SakuraDuskSettings LoadSaved()
    {
        var s = new SakuraDuskSettings();
        s.Load();
        return s;
    }
}
