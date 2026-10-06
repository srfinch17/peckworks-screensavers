using Peckworks.Screensavers.Core;

namespace Christmas;

/// <summary>
/// Christmas's knobs: the same four as Sakura, but for snow. Saved under
/// HKEY_CURRENT_USER\Software\Peckworks\Screensavers\Christmas. The base class
/// does the saving; the settings dialog makes one slider per knob.
/// </summary>
public sealed class ChristmasSettings : ScreensaverSettings
{
    private readonly IntSetting _density, _speed, _wind, _size, _surprises;

    public ChristmasSettings() : base("Christmas")
    {
        _density = Add(nameof(DensityPercent), "Snow", 20, 300, 100, "%");
        _speed = Add(nameof(FallSpeedPercent), "Fall speed", 25, 300, 100, "%");
        _wind = Add(nameof(WindPercent), "Breeze", 0, 300, 100, "%");
        _size = Add(nameof(FlakeSizePercent), "Flake size", 50, 200, 100, "%");
        _surprises = Add(nameof(SurprisePercent), "Surprises", 0, 300, 100, "%");
    }

    /// <summary>How many snowflakes are falling at once.</summary>
    public int DensityPercent => _density.Value;

    /// <summary>How fast the snow falls. 100 = a slow, gentle snowfall.</summary>
    public int FallSpeedPercent => _speed.Value;

    /// <summary>How hard the breeze pushes the snow sideways. 0 = still air.</summary>
    public int WindPercent => _wind.Value;

    /// <summary>Snowflake size.</summary>
    public int FlakeSizePercent => _size.Value;

    /// <summary>How often a happening (a fox, a snowman, fireworks...) comes on. 100 = one every 5 to 12 seconds, 0 = never.</summary>
    public int SurprisePercent => _surprises.Value;

    public static ChristmasSettings LoadSaved()
    {
        var s = new ChristmasSettings();
        s.Load();
        return s;
    }
}
