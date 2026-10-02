using Peckworks.Screensavers.Core;

namespace Halloween;

/// <summary>
/// Halloween's knobs: the same four as Sakura (but for leaves), plus how many
/// bats are out. Saved under
/// HKEY_CURRENT_USER\Software\Peckworks\Screensavers\Halloween. The base class
/// does the saving; the settings dialog makes one slider per knob.
/// </summary>
public sealed class HalloweenSettings : ScreensaverSettings
{
    private readonly IntSetting _density, _speed, _wind, _size, _bats;

    public HalloweenSettings() : base("Halloween")
    {
        _density = Add(nameof(DensityPercent), "Leaves", 20, 300, 100, "%");
        _speed = Add(nameof(FallSpeedPercent), "Fall speed", 25, 300, 100, "%");
        _wind = Add(nameof(WindPercent), "Breeze", 0, 300, 100, "%");
        _size = Add(nameof(LeafSizePercent), "Leaf size", 50, 200, 100, "%");
        _bats = Add(nameof(BatCount), "Bats", 0, 40, 14);
    }

    /// <summary>How many leaves are falling at once.</summary>
    public int DensityPercent => _density.Value;

    /// <summary>How fast leaves fall. 100 = a slow drift.</summary>
    public int FallSpeedPercent => _speed.Value;

    /// <summary>How hard the breeze pushes leaves sideways. 0 = still air.</summary>
    public int WindPercent => _wind.Value;

    /// <summary>Leaf size.</summary>
    public int LeafSizePercent => _size.Value;

    /// <summary>How many bats are flying. 0 = none.</summary>
    public int BatCount => _bats.Value;

    public static HalloweenSettings LoadSaved()
    {
        var s = new HalloweenSettings();
        s.Load();
        return s;
    }
}
